using EMS.Application.Common.Exceptions;
using EMS.Application.Common.Security;
using EMS.Domain.Common;
using EMS.Domain.Repositories;
using EMS.Shared.Assets;
using EMS.Shared.Authorization;
using FluentValidation;
using MediatR;

namespace EMS.Application.Assets;

/// <summary>Edits descriptive fields. Category is fixed at registration (it drives the asset code and depreciation policy); status changes go through the lifecycle commands.</summary>
[RequiresPermission(Permissions.Assets.Edit)]
public sealed record UpdateAssetCommand(int Id, UpdateAssetRequest Data) : IRequest;

internal sealed class UpdateAssetCommandValidator : AbstractValidator<UpdateAssetCommand>
{
    public UpdateAssetCommandValidator(
        IDepartmentRepository departments,
        ISupplierRepository suppliers)
    {
        RuleFor(x => x.Data.Name).NotEmpty().MaximumLength(200).OverridePropertyName("Name");
        RuleFor(x => x.Data.SerialNumber).MaximumLength(100).OverridePropertyName("SerialNumber");
        RuleFor(x => x.Data.Model).MaximumLength(100).OverridePropertyName("Model");
        RuleFor(x => x.Data.Notes).MaximumLength(1000).OverridePropertyName("Notes");
        RuleFor(x => x.Data.PurchaseCost)
            .GreaterThan(0).WithMessage("Purchase cost must be greater than zero.")
            .OverridePropertyName("PurchaseCost");
        RuleFor(x => x.Data.PurchaseDate).NotEmpty().OverridePropertyName("PurchaseDate");
        RuleFor(x => x.Data.WarrantyExpiryDate)
            .GreaterThanOrEqualTo(x => x.Data.PurchaseDate)
            .WithMessage("Warranty expiry cannot be before the purchase date.")
            .OverridePropertyName("WarrantyExpiryDate")
            .When(x => x.Data.WarrantyExpiryDate.HasValue);
        RuleFor(x => x.Data.RowVersion).NotEmpty().OverridePropertyName("RowVersion");

        RuleFor(x => x.Data.DepartmentId)
            .MustAsync(departments.ExistsAsync)
            .WithMessage("Selected department does not exist.")
            .OverridePropertyName("DepartmentId");
        RuleFor(x => x.Data.SupplierId!.Value)
            .MustAsync(suppliers.ExistsAsync)
            .WithMessage("Selected supplier does not exist.")
            .OverridePropertyName("SupplierId")
            .When(x => x.Data.SupplierId.HasValue);
    }
}

internal sealed class UpdateAssetCommandHandler(
    IAssetRepository assets,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateAssetCommand>
{
    public async Task Handle(UpdateAssetCommand request, CancellationToken cancellationToken)
    {
        var asset = await assets.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Asset), request.Id);

        var data = request.Data;
        assets.SetOriginalRowVersion(asset, Convert.FromBase64String(data.RowVersion));

        asset.Name = data.Name.Trim();
        asset.DepartmentId = data.DepartmentId;
        asset.SupplierId = data.SupplierId;
        asset.SerialNumber = data.SerialNumber?.Trim();
        asset.Model = data.Model?.Trim();
        asset.PurchaseDate = data.PurchaseDate;
        asset.PurchaseCost = data.PurchaseCost;
        asset.WarrantyExpiryDate = data.WarrantyExpiryDate;
        asset.Notes = data.Notes?.Trim();

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
