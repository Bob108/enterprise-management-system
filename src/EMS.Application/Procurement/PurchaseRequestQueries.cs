using EMS.Application.Common.Exceptions;
using EMS.Application.Common.Interfaces;
using EMS.Domain.Repositories;
using EMS.Shared.Authorization;
using EMS.Shared.Common;
using EMS.Shared.Enums;
using EMS.Shared.Procurement;
using MediatR;

namespace EMS.Application.Procurement;

// No [RequiresPermission]: everyone may raise requests, so everyone may list — but users
// without procurement.view are hard-scoped to their own requests inside the handler.

public sealed record GetPurchaseRequestsQuery(
    string? Search,
    PurchaseRequestStatus? Status,
    bool MineOnly = false,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<PurchaseRequestListItemDto>>;

public sealed record GetPurchaseRequestByIdQuery(int Id) : IRequest<PurchaseRequestDetailDto>;

internal sealed class PurchaseRequestQueryHandlers(
    IProcurementRepository procurement,
    ICurrentUser currentUser) :
    IRequestHandler<GetPurchaseRequestsQuery, PagedResult<PurchaseRequestListItemDto>>,
    IRequestHandler<GetPurchaseRequestByIdQuery, PurchaseRequestDetailDto>
{
    private const int MaxPageSize = 100;

    public async Task<PagedResult<PurchaseRequestListItemDto>> Handle(
        GetPurchaseRequestsQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);

        var canViewAll = currentUser.HasPermission(Permissions.Procurement.View);
        int? requesterFilter = request.MineOnly || !canViewAll ? currentUser.UserId : null;

        var (items, totalCount) = await procurement.SearchRequestsAsync(
            new PurchaseRequestSearchCriteria(
                request.Search?.Trim(), request.Status, requesterFilter, page, pageSize),
            cancellationToken);

        var dtos = items
            .Select(r => new PurchaseRequestListItemDto(
                r.Id, r.RequestNumber, r.RequestedByName, r.Department.Name,
                r.TotalEstimatedCost, r.Status, r.RequiresSecondApproval,
                r.AwaitingSecondApproval, r.CreatedAtUtc))
            .ToList();

        return new PagedResult<PurchaseRequestListItemDto>(dtos, page, pageSize, totalCount);
    }

    public async Task<PurchaseRequestDetailDto> Handle(
        GetPurchaseRequestByIdQuery request, CancellationToken cancellationToken)
    {
        var pr = await procurement.GetRequestByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.PurchaseRequest), request.Id);

        var isOwner = pr.RequestedByUserId == currentUser.UserId;
        if (!isOwner && !currentUser.HasPermission(Permissions.Procurement.View))
        {
            throw new ForbiddenAccessException(Permissions.Procurement.View);
        }

        return pr.ToDetailDto();
    }
}
