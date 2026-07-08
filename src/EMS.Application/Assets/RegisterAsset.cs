using EMS.Application.Common.Security;
using EMS.Domain.Common;
using EMS.Domain.Entities;
using EMS.Domain.Repositories;
using EMS.Shared.Assets;
using EMS.Shared.Authorization;
using FluentValidation;
using MediatR;

namespace EMS.Application.Assets;

[RequiresPermission(Permissions.Assets.Create)]
public sealed record RegisterAssetCommand(RegisterAssetRequest Data) : IRequest<int>;

internal sealed class RegisterAssetCommandValidator : AbstractValidator<RegisterAssetCommand>
{
    public RegisterAssetCommandValidator(
        IAssetCategoryRepository categories,
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

        RuleFor(x => x.Data.CategoryId)
            .MustAsync(categories.ExistsAsync)
            .WithMessage("Selected category does not exist.")
            .OverridePropertyName("CategoryId");
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

internal sealed class RegisterAssetCommandHandler(
    IAssetRepository assets,
    IAssetCategoryRepository categories,
    IUnitOfWork unitOfWork) : IRequestHandler<RegisterAssetCommand, int>
{
    public async Task<int> Handle(RegisterAssetCommand request, CancellationToken cancellationToken)
    {
        var data = request.Data;
        var category = await categories.GetByIdAsync(data.CategoryId, cancellationToken)
            ?? throw new Common.Exceptions.NotFoundException(nameof(AssetCategory), data.CategoryId);

        var asset = new Asset
        {
            AssetCode = await assets.GetNextAssetCodeAsync(category.CodePrefix, cancellationToken),
            Name = data.Name.Trim(),
            CategoryId = data.CategoryId,
            DepartmentId = data.DepartmentId,
            SupplierId = data.SupplierId,
            SerialNumber = data.SerialNumber?.Trim(),
            Model = data.Model?.Trim(),
            PurchaseDate = data.PurchaseDate,
            PurchaseCost = data.PurchaseCost,
            WarrantyExpiryDate = data.WarrantyExpiryDate,
            Notes = data.Notes?.Trim(),
        };

        assets.Add(asset);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return asset.Id;
    }
}
