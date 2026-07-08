using EMS.Application.Common.Exceptions;
using EMS.Application.Common.Security;
using EMS.Domain.Common;
using EMS.Domain.Entities;
using EMS.Domain.Repositories;
using EMS.Shared.Assets;
using EMS.Shared.Authorization;
using FluentValidation;
using MediatR;

namespace EMS.Application.Suppliers;

/// <summary>No permission attribute: every authenticated user may read the list (dropdowns).</summary>
public sealed record GetSuppliersQuery : IRequest<IReadOnlyList<SupplierDto>>;

[RequiresPermission(Permissions.Administration.Settings)]
public sealed record CreateSupplierCommand(SaveSupplierRequest Data) : IRequest<int>;

[RequiresPermission(Permissions.Administration.Settings)]
public sealed record UpdateSupplierCommand(int Id, SaveSupplierRequest Data) : IRequest;

[RequiresPermission(Permissions.Administration.Settings)]
public sealed record DeleteSupplierCommand(int Id) : IRequest;

internal sealed class SaveSupplierValidator : AbstractValidator<SaveSupplierRequest>
{
    public SaveSupplierValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ContactPerson).MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(256)
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone).MaximumLength(32);
        RuleFor(x => x.Address).MaximumLength(500);
    }
}

internal sealed class CreateSupplierCommandValidator : AbstractValidator<CreateSupplierCommand>
{
    public CreateSupplierCommandValidator(ISupplierRepository suppliers)
    {
        RuleFor(x => x.Data).SetValidator(new SaveSupplierValidator()).OverridePropertyName("");
        RuleFor(x => x.Data.Name)
            .MustAsync(async (name, ct) => !await suppliers.NameTakenAsync(name, null, ct))
            .WithMessage("A supplier with this name already exists.")
            .OverridePropertyName("Name")
            .When(x => !string.IsNullOrWhiteSpace(x.Data.Name));
    }
}

internal sealed class UpdateSupplierCommandValidator : AbstractValidator<UpdateSupplierCommand>
{
    public UpdateSupplierCommandValidator(ISupplierRepository suppliers)
    {
        RuleFor(x => x.Data).SetValidator(new SaveSupplierValidator()).OverridePropertyName("");
        RuleFor(x => x)
            .MustAsync(async (cmd, ct) => !await suppliers.NameTakenAsync(cmd.Data.Name, cmd.Id, ct))
            .WithMessage("A supplier with this name already exists.")
            .OverridePropertyName("Name")
            .When(x => !string.IsNullOrWhiteSpace(x.Data.Name));
    }
}

internal sealed class SupplierHandlers(
    ISupplierRepository suppliers,
    IUnitOfWork unitOfWork) :
    IRequestHandler<GetSuppliersQuery, IReadOnlyList<SupplierDto>>,
    IRequestHandler<CreateSupplierCommand, int>,
    IRequestHandler<UpdateSupplierCommand>,
    IRequestHandler<DeleteSupplierCommand>
{
    public async Task<IReadOnlyList<SupplierDto>> Handle(
        GetSuppliersQuery request, CancellationToken cancellationToken)
    {
        var items = await suppliers.GetAllWithAssetCountAsync(cancellationToken);
        return items
            .Select(x => new SupplierDto(
                x.Supplier.Id, x.Supplier.Name, x.Supplier.ContactPerson,
                x.Supplier.Email, x.Supplier.Phone, x.Supplier.Address, x.AssetCount))
            .ToList();
    }

    public async Task<int> Handle(CreateSupplierCommand request, CancellationToken cancellationToken)
    {
        var supplier = new Supplier
        {
            Name = request.Data.Name.Trim(),
            ContactPerson = request.Data.ContactPerson?.Trim(),
            Email = request.Data.Email?.Trim(),
            Phone = request.Data.Phone?.Trim(),
            Address = request.Data.Address?.Trim(),
        };

        suppliers.Add(supplier);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return supplier.Id;
    }

    public async Task Handle(UpdateSupplierCommand request, CancellationToken cancellationToken)
    {
        var supplier = await suppliers.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Supplier), request.Id);

        supplier.Name = request.Data.Name.Trim();
        supplier.ContactPerson = request.Data.ContactPerson?.Trim();
        supplier.Email = request.Data.Email?.Trim();
        supplier.Phone = request.Data.Phone?.Trim();
        supplier.Address = request.Data.Address?.Trim();

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task Handle(DeleteSupplierCommand request, CancellationToken cancellationToken)
    {
        var supplier = await suppliers.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Supplier), request.Id);

        var assetCount = await suppliers.CountAssetsAsync(request.Id, cancellationToken);
        if (assetCount > 0)
        {
            throw new ConflictException(
                $"Supplier '{supplier.Name}' is referenced by {assetCount} asset(s) and cannot be deleted.");
        }

        suppliers.Remove(supplier); // soft delete via audit interceptor
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
