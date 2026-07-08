using EMS.Application.Common.Exceptions;
using EMS.Application.Common.Security;
using EMS.Domain.Common;
using EMS.Domain.Entities;
using EMS.Domain.Repositories;
using EMS.Shared.Authorization;
using EMS.Shared.Inventory;
using FluentValidation;
using MediatR;

namespace EMS.Application.Warehouses;

/// <summary>No permission attribute: every authenticated user may read the list (dropdowns).</summary>
public sealed record GetWarehousesQuery : IRequest<IReadOnlyList<WarehouseDto>>;

[RequiresPermission(Permissions.Administration.Settings)]
public sealed record CreateWarehouseCommand(SaveWarehouseRequest Data) : IRequest<int>;

[RequiresPermission(Permissions.Administration.Settings)]
public sealed record UpdateWarehouseCommand(int Id, SaveWarehouseRequest Data) : IRequest;

[RequiresPermission(Permissions.Administration.Settings)]
public sealed record DeleteWarehouseCommand(int Id) : IRequest;

internal sealed class SaveWarehouseValidator : AbstractValidator<SaveWarehouseRequest>
{
    public SaveWarehouseValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(16);
        RuleFor(x => x.Location).MaximumLength(200);
    }
}

internal sealed class CreateWarehouseCommandValidator : AbstractValidator<CreateWarehouseCommand>
{
    public CreateWarehouseCommandValidator(IWarehouseRepository warehouses)
    {
        RuleFor(x => x.Data).SetValidator(new SaveWarehouseValidator()).OverridePropertyName("");
        RuleFor(x => x.Data)
            .MustAsync(async (data, ct) =>
                !await warehouses.NameOrCodeTakenAsync(data.Name, data.Code.ToUpperInvariant(), null, ct))
            .WithMessage("A warehouse with the same name or code already exists.")
            .OverridePropertyName("Name")
            .When(x => !string.IsNullOrWhiteSpace(x.Data.Name) && !string.IsNullOrWhiteSpace(x.Data.Code));
    }
}

internal sealed class UpdateWarehouseCommandValidator : AbstractValidator<UpdateWarehouseCommand>
{
    public UpdateWarehouseCommandValidator(IWarehouseRepository warehouses)
    {
        RuleFor(x => x.Data).SetValidator(new SaveWarehouseValidator()).OverridePropertyName("");
        RuleFor(x => x)
            .MustAsync(async (cmd, ct) =>
                !await warehouses.NameOrCodeTakenAsync(cmd.Data.Name, cmd.Data.Code.ToUpperInvariant(), cmd.Id, ct))
            .WithMessage("A warehouse with the same name or code already exists.")
            .OverridePropertyName("Name")
            .When(x => !string.IsNullOrWhiteSpace(x.Data.Name) && !string.IsNullOrWhiteSpace(x.Data.Code));
    }
}

internal sealed class WarehouseHandlers(
    IWarehouseRepository warehouses,
    IUnitOfWork unitOfWork) :
    IRequestHandler<GetWarehousesQuery, IReadOnlyList<WarehouseDto>>,
    IRequestHandler<CreateWarehouseCommand, int>,
    IRequestHandler<UpdateWarehouseCommand>,
    IRequestHandler<DeleteWarehouseCommand>
{
    public async Task<IReadOnlyList<WarehouseDto>> Handle(
        GetWarehousesQuery request, CancellationToken cancellationToken)
    {
        var items = await warehouses.GetAllWithItemCountAsync(cancellationToken);
        return items
            .Select(x => new WarehouseDto(
                x.Warehouse.Id, x.Warehouse.Name, x.Warehouse.Code, x.Warehouse.Location, x.StockedItemCount))
            .ToList();
    }

    public async Task<int> Handle(CreateWarehouseCommand request, CancellationToken cancellationToken)
    {
        var warehouse = new Warehouse
        {
            Name = request.Data.Name.Trim(),
            Code = request.Data.Code.Trim().ToUpperInvariant(),
            Location = request.Data.Location?.Trim(),
        };

        warehouses.Add(warehouse);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return warehouse.Id;
    }

    public async Task Handle(UpdateWarehouseCommand request, CancellationToken cancellationToken)
    {
        var warehouse = await warehouses.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Warehouse), request.Id);

        warehouse.Name = request.Data.Name.Trim();
        warehouse.Code = request.Data.Code.Trim().ToUpperInvariant();
        warehouse.Location = request.Data.Location?.Trim();

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task Handle(DeleteWarehouseCommand request, CancellationToken cancellationToken)
    {
        var warehouse = await warehouses.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Warehouse), request.Id);

        if (await warehouses.HasStockAsync(request.Id, cancellationToken))
        {
            throw new ConflictException(
                $"Warehouse '{warehouse.Name}' still holds stock and cannot be deleted.");
        }

        warehouses.Remove(warehouse); // soft delete via audit interceptor
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
