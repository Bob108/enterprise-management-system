using EMS.Application.Common.Security;
using EMS.Domain.Repositories;
using EMS.Shared.Assets;
using EMS.Shared.Authorization;
using EMS.Shared.Common;
using EMS.Shared.Enums;
using MediatR;

namespace EMS.Application.Assets;

[RequiresPermission(Permissions.Assets.View)]
public sealed record GetAssetsQuery(
    string? Search,
    int? CategoryId,
    int? DepartmentId,
    AssetStatus? Status,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<AssetListItemDto>>;

internal sealed class GetAssetsQueryHandler(IAssetRepository assets)
    : IRequestHandler<GetAssetsQuery, PagedResult<AssetListItemDto>>
{
    private const int MaxPageSize = 100;

    public async Task<PagedResult<AssetListItemDto>> Handle(
        GetAssetsQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);

        var (rows, totalCount) = await assets.SearchAsync(
            new AssetSearchCriteria(
                request.Search?.Trim(), request.CategoryId, request.DepartmentId,
                request.Status, page, pageSize),
            cancellationToken);

        var items = rows
            .Select(r => new AssetListItemDto(
                r.Asset.Id,
                r.Asset.AssetCode,
                r.Asset.Name,
                r.Asset.Category.Name,
                r.Asset.Department.Name,
                r.Asset.Status,
                r.Asset.CurrentAssignee?.FullName,
                r.Asset.PurchaseDate,
                r.Asset.PurchaseCost,
                r.Asset.PurchaseCost - r.DepreciatedTotal))
            .ToList();

        return new PagedResult<AssetListItemDto>(items, page, pageSize, totalCount);
    }
}
