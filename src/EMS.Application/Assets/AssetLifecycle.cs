using EMS.Application.Common.Exceptions;
using EMS.Application.Common.Interfaces;
using EMS.Application.Common.Security;
using EMS.Domain.Common;
using EMS.Domain.Repositories;
using EMS.Shared.Authorization;
using FluentValidation;
using MediatR;

namespace EMS.Application.Assets;

// Lifecycle commands: thin wrappers around the Asset entity's state machine (design §6.2).
// Illegal transitions raise DomainException from the entity → HTTP 409.

[RequiresPermission(Permissions.Assets.Assign)]
public sealed record AssignAssetCommand(int Id, int EmployeeId, string? ConditionNotes) : IRequest;

[RequiresPermission(Permissions.Assets.Assign)]
public sealed record ReturnAssetCommand(int Id, string? ConditionNotes) : IRequest;

[RequiresPermission(Permissions.Assets.Transfer)]
public sealed record TransferAssetCommand(int Id, int ToDepartmentId, string? Reason) : IRequest;

[RequiresPermission(Permissions.Assets.Edit)]
public sealed record MarkAssetUnderRepairCommand(int Id) : IRequest;

[RequiresPermission(Permissions.Assets.Edit)]
public sealed record MarkAssetRepairedCommand(int Id) : IRequest;

[RequiresPermission(Permissions.Assets.Edit)]
public sealed record ReportAssetLostCommand(int Id) : IRequest;

[RequiresPermission(Permissions.Assets.Edit)]
public sealed record RecoverAssetCommand(int Id) : IRequest;

internal sealed class AssignAssetCommandValidator : AbstractValidator<AssignAssetCommand>
{
    public AssignAssetCommandValidator(IEmployeeRepository employees)
    {
        RuleFor(x => x.ConditionNotes).MaximumLength(500);
        RuleFor(x => x.EmployeeId)
            .MustAsync(employees.ExistsAsync)
            .WithMessage("Selected employee does not exist.");
    }
}

internal sealed class TransferAssetCommandValidator : AbstractValidator<TransferAssetCommand>
{
    public TransferAssetCommandValidator(IDepartmentRepository departments)
    {
        RuleFor(x => x.Reason).MaximumLength(500);
        RuleFor(x => x.ToDepartmentId)
            .MustAsync(departments.ExistsAsync)
            .WithMessage("Target department does not exist.");
    }
}

internal sealed class AssetLifecycleHandlers(
    IAssetRepository assets,
    IUnitOfWork unitOfWork,
    IDateTime clock) :
    IRequestHandler<AssignAssetCommand>,
    IRequestHandler<ReturnAssetCommand>,
    IRequestHandler<TransferAssetCommand>,
    IRequestHandler<MarkAssetUnderRepairCommand>,
    IRequestHandler<MarkAssetRepairedCommand>,
    IRequestHandler<ReportAssetLostCommand>,
    IRequestHandler<RecoverAssetCommand>
{
    public Task Handle(AssignAssetCommand request, CancellationToken ct)
        => Mutate(request.Id, a => a.AssignTo(request.EmployeeId, request.ConditionNotes, Today()), ct);

    public Task Handle(ReturnAssetCommand request, CancellationToken ct)
        => Mutate(request.Id, a => a.Return(request.ConditionNotes, Today()), ct);

    public Task Handle(TransferAssetCommand request, CancellationToken ct)
        => Mutate(request.Id, a => a.TransferTo(request.ToDepartmentId, request.Reason, Today()), ct);

    public Task Handle(MarkAssetUnderRepairCommand request, CancellationToken ct)
        => Mutate(request.Id, a => a.MarkUnderRepair(), ct);

    public Task Handle(MarkAssetRepairedCommand request, CancellationToken ct)
        => Mutate(request.Id, a => a.MarkRepaired(), ct);

    public Task Handle(ReportAssetLostCommand request, CancellationToken ct)
        => Mutate(request.Id, a => a.ReportLost(Today()), ct);

    public Task Handle(RecoverAssetCommand request, CancellationToken ct)
        => Mutate(request.Id, a => a.Recover(), ct);

    private DateOnly Today() => DateOnly.FromDateTime(clock.UtcNow);

    private async Task Mutate(int id, Action<Domain.Entities.Asset> action, CancellationToken ct)
    {
        var asset = await assets.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Domain.Entities.Asset), id);
        action(asset);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
