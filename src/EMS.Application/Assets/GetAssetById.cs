using EMS.Application.Common.Exceptions;
using EMS.Application.Common.Security;
using EMS.Domain.Repositories;
using EMS.Shared.Assets;
using EMS.Shared.Authorization;
using MediatR;

namespace EMS.Application.Assets;

[RequiresPermission(Permissions.Assets.View)]
public sealed record GetAssetByIdQuery(int Id) : IRequest<AssetDetailDto>;

internal sealed class GetAssetByIdQueryHandler(
    IAssetRepository assets,
    IDepartmentRepository departments) : IRequestHandler<GetAssetByIdQuery, AssetDetailDto>
{
    public async Task<AssetDetailDto> Handle(GetAssetByIdQuery request, CancellationToken cancellationToken)
    {
        var asset = await assets.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Asset), request.Id);

        var depreciated = await assets.GetDepreciatedTotalAsync(asset.Id, cancellationToken);
        var departmentNames = await departments.GetNamesAsync(cancellationToken);

        return new AssetDetailDto(
            asset.Id,
            asset.AssetCode,
            asset.Name,
            asset.CategoryId,
            asset.Category.Name,
            asset.DepartmentId,
            asset.Department.Name,
            asset.SupplierId,
            asset.Supplier?.Name,
            asset.SerialNumber,
            asset.Model,
            asset.PurchaseDate,
            asset.PurchaseCost,
            asset.PurchaseCost - depreciated,
            asset.WarrantyExpiryDate,
            asset.Status,
            asset.CurrentAssigneeEmployeeId,
            asset.CurrentAssignee?.FullName,
            asset.Notes,
            asset.Assignments
                .OrderByDescending(a => a.AssignedOn)
                .Select(a => new AssetAssignmentDto(
                    a.Id, a.Employee.FullName, a.AssignedOn, a.ReturnedOn, a.ConditionOut, a.ConditionIn))
                .ToList(),
            asset.Transfers
                .OrderByDescending(t => t.TransferredOn)
                .Select(t => new AssetTransferDto(
                    t.Id,
                    departmentNames.GetValueOrDefault(t.FromDepartmentId, "(removed)"),
                    departmentNames.GetValueOrDefault(t.ToDepartmentId, "(removed)"),
                    t.TransferredOn,
                    t.Reason))
                .ToList(),
            asset.Disposal is { } d
                ? new AssetDisposalDto(d.DisposedOn, d.Method, d.Proceeds, d.GainLoss, d.Reason)
                : null,
            Convert.ToBase64String(asset.RowVersion));
    }
}
