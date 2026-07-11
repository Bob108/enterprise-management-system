using EMS.Application.Common.Exceptions;
using EMS.Application.Common.Interfaces;
using EMS.Application.Common.Security;
using EMS.Domain.Common;
using EMS.Domain.Entities;
using EMS.Domain.Repositories;
using EMS.Shared.Authorization;
using EMS.Shared.Common;
using EMS.Shared.Enums;
using EMS.Shared.Procurement;
using FluentValidation;
using MediatR;

namespace EMS.Application.Procurement;

[RequiresPermission(Permissions.Procurement.View)]
public sealed record GetPurchaseOrdersQuery(
    string? Search,
    PurchaseOrderStatus? Status,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<PurchaseOrderListItemDto>>;

[RequiresPermission(Permissions.Procurement.View)]
public sealed record GetPurchaseOrderByIdQuery(int Id) : IRequest<PurchaseOrderDetailDto>;

[RequiresPermission(Permissions.Procurement.ManagePurchaseOrders)]
public sealed record CreatePurchaseOrderCommand(int PurchaseRequestId, CreatePurchaseOrderRequest Data)
    : IRequest<int>;

[RequiresPermission(Permissions.Procurement.ManagePurchaseOrders)]
public sealed record IssuePurchaseOrderCommand(int Id) : IRequest;

[RequiresPermission(Permissions.Procurement.ManagePurchaseOrders)]
public sealed record CancelPurchaseOrderCommand(int Id) : IRequest;

[RequiresPermission(Permissions.Procurement.ManagePurchaseOrders)]
public sealed record ClosePurchaseOrderCommand(int Id) : IRequest;

internal sealed class CreatePurchaseOrderCommandValidator : AbstractValidator<CreatePurchaseOrderCommand>
{
    public CreatePurchaseOrderCommandValidator(ISupplierRepository suppliers)
    {
        RuleFor(x => x.Data.SupplierId)
            .MustAsync(suppliers.ExistsAsync)
            .WithMessage("Selected supplier does not exist.")
            .OverridePropertyName("SupplierId");
        RuleFor(x => x.Data.Notes).MaximumLength(1000).OverridePropertyName("Notes");
        RuleFor(x => x.Data.Lines)
            .NotEmpty().WithMessage("Price every request line.")
            .Must(l => l.Select(x => x.PurchaseRequestLineId).Distinct().Count() == l.Count)
            .WithMessage("Duplicate request line.")
            .OverridePropertyName("Lines");
        RuleForEach(x => x.Data.Lines).ChildRules(line =>
            line.RuleFor(l => l.UnitPrice).GreaterThan(0)).OverridePropertyName("Lines");
    }
}

internal sealed class PurchaseOrderHandlers(
    IProcurementRepository procurement,
    IUnitOfWork unitOfWork,
    IDateTime clock) :
    IRequestHandler<GetPurchaseOrdersQuery, PagedResult<PurchaseOrderListItemDto>>,
    IRequestHandler<GetPurchaseOrderByIdQuery, PurchaseOrderDetailDto>,
    IRequestHandler<CreatePurchaseOrderCommand, int>,
    IRequestHandler<IssuePurchaseOrderCommand>,
    IRequestHandler<CancelPurchaseOrderCommand>,
    IRequestHandler<ClosePurchaseOrderCommand>
{
    private const int MaxPageSize = 100;

    public async Task<PagedResult<PurchaseOrderListItemDto>> Handle(
        GetPurchaseOrdersQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);

        var (items, totalCount) = await procurement.SearchOrdersAsync(
            new PurchaseOrderSearchCriteria(request.Search?.Trim(), request.Status, page, pageSize),
            cancellationToken);

        var dtos = items
            .Select(o => new PurchaseOrderListItemDto(
                o.Id, o.OrderNumber, o.PurchaseRequest.RequestNumber, o.Supplier.Name,
                o.TotalValue, o.Status, o.CreatedAtUtc))
            .ToList();

        return new PagedResult<PurchaseOrderListItemDto>(dtos, page, pageSize, totalCount);
    }

    public async Task<PurchaseOrderDetailDto> Handle(
        GetPurchaseOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var order = await LoadAsync(request.Id, cancellationToken);
        return order.ToDetailDto();
    }

    public async Task<int> Handle(CreatePurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        var pr = await procurement.GetRequestByIdAsync(request.PurchaseRequestId, cancellationToken)
            ?? throw new NotFoundException(nameof(PurchaseRequest), request.PurchaseRequestId);

        var prices = request.Data.Lines.ToDictionary(l => l.PurchaseRequestLineId, l => l.UnitPrice);
        var unpriced = pr.Lines.Where(l => !prices.ContainsKey(l.Id)).Select(l => l.Description).ToList();
        if (unpriced.Count > 0)
        {
            throw new Common.Exceptions.ValidationException(
            [
                new FluentValidation.Results.ValidationFailure(
                    "Lines", $"Missing prices for: {string.Join(", ", unpriced)}"),
            ]);
        }

        var order = new PurchaseOrder
        {
            OrderNumber = await procurement.GetNextOrderNumberAsync(cancellationToken),
            PurchaseRequestId = pr.Id,
            SupplierId = request.Data.SupplierId,
            ExpectedDate = request.Data.ExpectedDate,
            Notes = request.Data.Notes?.Trim(),
            Lines = pr.Lines.Select(l => new PurchaseOrderLine
            {
                PurchaseRequestLineId = l.Id,
                Description = l.Description,
                Nature = l.Nature,
                AssetCategoryId = l.AssetCategoryId,
                InventoryItemId = l.InventoryItemId,
                OrderedQuantity = l.Quantity,
                UnitPrice = prices[l.Id],
            }).ToList(),
        };

        procurement.AddOrder(order);
        await unitOfWork.SaveChangesAsync(cancellationToken); // order id needed for the back-link

        pr.MarkConverted(order.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return order.Id;
    }

    public async Task Handle(IssuePurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await LoadAsync(request.Id, cancellationToken);
        order.Issue(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task Handle(CancelPurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await LoadAsync(request.Id, cancellationToken);
        order.Cancel();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task Handle(ClosePurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await LoadAsync(request.Id, cancellationToken);
        order.Close();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<PurchaseOrder> LoadAsync(int id, CancellationToken cancellationToken)
        => await procurement.GetOrderByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(PurchaseOrder), id);
}
