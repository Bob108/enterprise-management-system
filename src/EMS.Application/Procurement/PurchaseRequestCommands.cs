using EMS.Application.Common;
using EMS.Application.Common.Exceptions;
using EMS.Application.Common.Interfaces;
using EMS.Application.Common.Security;
using EMS.Domain.Common;
using EMS.Domain.Entities;
using EMS.Domain.Repositories;
using EMS.Shared.Authorization;
using EMS.Shared.Enums;
using EMS.Shared.Procurement;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;

namespace EMS.Application.Procurement;

[RequiresPermission(Permissions.Procurement.Raise)]
public sealed record CreatePurchaseRequestCommand(SavePurchaseRequestRequest Data) : IRequest<int>;

/// <summary>Draft-only, owner-only. Lines are replaced wholesale.</summary>
[RequiresPermission(Permissions.Procurement.Raise)]
public sealed record UpdatePurchaseRequestCommand(int Id, SavePurchaseRequestRequest Data) : IRequest;

[RequiresPermission(Permissions.Procurement.Raise)]
public sealed record SubmitPurchaseRequestCommand(int Id) : IRequest;

/// <summary>Stage-aware: first call needs approve.l1, the second (threshold) call approve.l2 — checked in the handler.</summary>
public sealed record ApprovePurchaseRequestCommand(int Id) : IRequest;

public sealed record RejectPurchaseRequestCommand(int Id, string Reason) : IRequest;

public sealed record ReturnPurchaseRequestCommand(int Id) : IRequest;

public sealed record CancelPurchaseRequestCommand(int Id) : IRequest;

internal sealed class SavePurchaseRequestValidator : AbstractValidator<SavePurchaseRequestRequest>
{
    public SavePurchaseRequestValidator(
        IDepartmentRepository departments,
        IAssetCategoryRepository categories,
        IInventoryRepository inventory)
    {
        RuleFor(x => x.DepartmentId)
            .MustAsync(departments.ExistsAsync)
            .WithMessage("Selected department does not exist.");
        RuleFor(x => x.Justification).MaximumLength(1000);
        RuleFor(x => x.Lines)
            .NotEmpty().WithMessage("Add at least one line.")
            .Must(l => l.Count <= 50).WithMessage("A request cannot exceed 50 lines.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.Description).NotEmpty().MaximumLength(300);
            line.RuleFor(l => l.Quantity).InclusiveBetween(1, 10_000);
            line.RuleFor(l => l.EstimatedUnitCost).GreaterThanOrEqualTo(0);
            line.RuleFor(l => l.Nature).IsInEnum();

            line.When(l => l.Nature == ItemNature.Asset, () =>
            {
                line.RuleFor(l => l.AssetCategoryId)
                    .NotNull().WithMessage("Asset lines need an asset category.");
                line.RuleFor(l => l.AssetCategoryId!.Value)
                    .MustAsync(categories.ExistsAsync)
                    .WithMessage("Selected asset category does not exist.")
                    .When(l => l.AssetCategoryId.HasValue);
            });
            line.When(l => l.Nature == ItemNature.Consumable, () =>
            {
                line.RuleFor(l => l.InventoryItemId)
                    .NotNull().WithMessage("Consumable lines need an inventory item.");
                line.RuleFor(l => l.InventoryItemId!.Value)
                    .MustAsync(async (id, ct) => await inventory.GetItemByIdAsync(id, ct) is not null)
                    .WithMessage("Selected inventory item does not exist.")
                    .When(l => l.InventoryItemId.HasValue);
            });
        });
    }
}

internal sealed class CreatePurchaseRequestCommandValidator : AbstractValidator<CreatePurchaseRequestCommand>
{
    public CreatePurchaseRequestCommandValidator(
        IDepartmentRepository departments, IAssetCategoryRepository categories, IInventoryRepository inventory)
        => RuleFor(x => x.Data)
            .SetValidator(new SavePurchaseRequestValidator(departments, categories, inventory))
            .OverridePropertyName("");
}

internal sealed class UpdatePurchaseRequestCommandValidator : AbstractValidator<UpdatePurchaseRequestCommand>
{
    public UpdatePurchaseRequestCommandValidator(
        IDepartmentRepository departments, IAssetCategoryRepository categories, IInventoryRepository inventory)
        => RuleFor(x => x.Data)
            .SetValidator(new SavePurchaseRequestValidator(departments, categories, inventory))
            .OverridePropertyName("");
}

internal sealed class RejectPurchaseRequestCommandValidator : AbstractValidator<RejectPurchaseRequestCommand>
{
    public RejectPurchaseRequestCommandValidator()
        => RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
}

internal sealed class PurchaseRequestCommandHandlers(
    IProcurementRepository procurement,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTime clock,
    IOptions<ProcurementOptions> options) :
    IRequestHandler<CreatePurchaseRequestCommand, int>,
    IRequestHandler<UpdatePurchaseRequestCommand>,
    IRequestHandler<SubmitPurchaseRequestCommand>,
    IRequestHandler<ApprovePurchaseRequestCommand>,
    IRequestHandler<RejectPurchaseRequestCommand>,
    IRequestHandler<ReturnPurchaseRequestCommand>,
    IRequestHandler<CancelPurchaseRequestCommand>
{
    public async Task<int> Handle(CreatePurchaseRequestCommand request, CancellationToken cancellationToken)
    {
        var pr = new PurchaseRequest
        {
            RequestNumber = await procurement.GetNextRequestNumberAsync(cancellationToken),
            RequestedByUserId = RequireUserId(),
            RequestedByName = currentUser.UserName ?? "unknown",
            DepartmentId = request.Data.DepartmentId,
            Justification = request.Data.Justification?.Trim(),
            Lines = MapLines(request.Data.Lines),
        };
        pr.RecalculateTotal();

        procurement.AddRequest(pr);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return pr.Id;
    }

    public async Task Handle(UpdatePurchaseRequestCommand request, CancellationToken cancellationToken)
    {
        var pr = await LoadOwnedAsync(request.Id, cancellationToken);
        pr.EnsureEditable();

        pr.DepartmentId = request.Data.DepartmentId;
        pr.Justification = request.Data.Justification?.Trim();
        pr.Lines.Clear(); // draft lines are replaced wholesale
        pr.Lines.AddRange(MapLines(request.Data.Lines));
        pr.RecalculateTotal();

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task Handle(SubmitPurchaseRequestCommand request, CancellationToken cancellationToken)
    {
        var pr = await LoadOwnedAsync(request.Id, cancellationToken);
        pr.Submit(options.Value.SecondApprovalThreshold);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task Handle(ApprovePurchaseRequestCommand request, CancellationToken cancellationToken)
    {
        var pr = await LoadAsync(request.Id, cancellationToken);

        // Stage decides the permission: L1 for the first signature, L2 for the second.
        var requiredPermission = pr.FirstApprovedAtUtc is null
            ? Permissions.Procurement.ApproveL1
            : Permissions.Procurement.ApproveL2;
        RequirePermission(requiredPermission);

        pr.Approve(RequireUserId(), currentUser.UserName ?? "unknown", clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task Handle(RejectPurchaseRequestCommand request, CancellationToken cancellationToken)
    {
        var pr = await LoadAsync(request.Id, cancellationToken);

        var requiredPermission = pr.FirstApprovedAtUtc is null
            ? Permissions.Procurement.ApproveL1
            : Permissions.Procurement.ApproveL2;
        RequirePermission(requiredPermission);

        pr.Reject(RequireUserId(), request.Reason.Trim());
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task Handle(ReturnPurchaseRequestCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(Permissions.Procurement.ApproveL1)
            && !currentUser.HasPermission(Permissions.Procurement.ApproveL2))
        {
            throw new ForbiddenAccessException(Permissions.Procurement.ApproveL1);
        }

        var pr = await LoadAsync(request.Id, cancellationToken);
        pr.ReturnToDraft();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task Handle(CancelPurchaseRequestCommand request, CancellationToken cancellationToken)
    {
        var pr = await LoadAsync(request.Id, cancellationToken);

        var isOwner = pr.RequestedByUserId == currentUser.UserId;
        if (!isOwner && !currentUser.HasPermission(Permissions.Procurement.ManagePurchaseOrders))
        {
            throw new ForbiddenAccessException(Permissions.Procurement.ManagePurchaseOrders);
        }

        pr.Cancel();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static List<PurchaseRequestLine> MapLines(List<SavePurchaseRequestLine> lines)
        => lines.Select(l => new PurchaseRequestLine
        {
            Description = l.Description.Trim(),
            Nature = l.Nature,
            AssetCategoryId = l.Nature == ItemNature.Asset ? l.AssetCategoryId : null,
            InventoryItemId = l.Nature == ItemNature.Consumable ? l.InventoryItemId : null,
            Quantity = l.Quantity,
            EstimatedUnitCost = l.EstimatedUnitCost,
        }).ToList();

    private async Task<PurchaseRequest> LoadAsync(int id, CancellationToken cancellationToken)
        => await procurement.GetRequestByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(PurchaseRequest), id);

    private async Task<PurchaseRequest> LoadOwnedAsync(int id, CancellationToken cancellationToken)
    {
        var pr = await LoadAsync(id, cancellationToken);
        if (pr.RequestedByUserId != currentUser.UserId)
        {
            throw new ForbiddenAccessException(Permissions.Procurement.Raise);
        }

        return pr;
    }

    private int RequireUserId()
        => currentUser.UserId ?? throw new UnauthorizedAccessException();

    private void RequirePermission(string permission)
    {
        if (!currentUser.HasPermission(permission))
        {
            throw new ForbiddenAccessException(permission);
        }
    }
}
