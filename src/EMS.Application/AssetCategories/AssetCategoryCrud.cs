using EMS.Application.Common.Exceptions;
using EMS.Application.Common.Security;
using EMS.Domain.Common;
using EMS.Domain.Entities;
using EMS.Domain.Repositories;
using EMS.Shared.Assets;
using EMS.Shared.Authorization;
using FluentValidation;
using MediatR;

namespace EMS.Application.AssetCategories;

/// <summary>No permission attribute: every authenticated user may read the list (dropdowns).</summary>
public sealed record GetAssetCategoriesQuery : IRequest<IReadOnlyList<AssetCategoryDto>>;

[RequiresPermission(Permissions.Administration.Settings)]
public sealed record CreateAssetCategoryCommand(SaveAssetCategoryRequest Data) : IRequest<int>;

[RequiresPermission(Permissions.Administration.Settings)]
public sealed record UpdateAssetCategoryCommand(int Id, SaveAssetCategoryRequest Data) : IRequest;

[RequiresPermission(Permissions.Administration.Settings)]
public sealed record DeleteAssetCategoryCommand(int Id) : IRequest;

internal sealed class SaveAssetCategoryValidator : AbstractValidator<SaveAssetCategoryRequest>
{
    public SaveAssetCategoryValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CodePrefix)
            .NotEmpty().Matches("^[A-Za-z]{2,6}$")
            .WithMessage("Code prefix must be 2–6 letters.");
        RuleFor(x => x.Method).IsInEnum();
        RuleFor(x => x.UsefulLifeMonths).InclusiveBetween(1, 600);
        RuleFor(x => x.ResidualRate).InclusiveBetween(0m, 0.9m)
            .WithMessage("Residual rate must be between 0 and 0.9 (90%).");
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

internal sealed class CreateAssetCategoryCommandValidator : AbstractValidator<CreateAssetCategoryCommand>
{
    public CreateAssetCategoryCommandValidator(IAssetCategoryRepository categories)
    {
        RuleFor(x => x.Data).SetValidator(new SaveAssetCategoryValidator()).OverridePropertyName("");
        RuleFor(x => x.Data)
            .MustAsync(async (data, ct) =>
                !await categories.NameOrPrefixTakenAsync(data.Name, data.CodePrefix.ToUpperInvariant(), null, ct))
            .WithMessage("A category with the same name or code prefix already exists.")
            .OverridePropertyName("Name")
            .When(x => !string.IsNullOrWhiteSpace(x.Data.Name) && !string.IsNullOrWhiteSpace(x.Data.CodePrefix));
    }
}

internal sealed class UpdateAssetCategoryCommandValidator : AbstractValidator<UpdateAssetCategoryCommand>
{
    public UpdateAssetCategoryCommandValidator(IAssetCategoryRepository categories)
    {
        RuleFor(x => x.Data).SetValidator(new SaveAssetCategoryValidator()).OverridePropertyName("");
        RuleFor(x => x)
            .MustAsync(async (cmd, ct) =>
                !await categories.NameOrPrefixTakenAsync(cmd.Data.Name, cmd.Data.CodePrefix.ToUpperInvariant(), cmd.Id, ct))
            .WithMessage("A category with the same name or code prefix already exists.")
            .OverridePropertyName("Name")
            .When(x => !string.IsNullOrWhiteSpace(x.Data.Name) && !string.IsNullOrWhiteSpace(x.Data.CodePrefix));
    }
}

internal sealed class AssetCategoryHandlers(
    IAssetCategoryRepository categories,
    IUnitOfWork unitOfWork) :
    IRequestHandler<GetAssetCategoriesQuery, IReadOnlyList<AssetCategoryDto>>,
    IRequestHandler<CreateAssetCategoryCommand, int>,
    IRequestHandler<UpdateAssetCategoryCommand>,
    IRequestHandler<DeleteAssetCategoryCommand>
{
    public async Task<IReadOnlyList<AssetCategoryDto>> Handle(
        GetAssetCategoriesQuery request, CancellationToken cancellationToken)
    {
        var items = await categories.GetAllWithAssetCountAsync(cancellationToken);
        return items
            .Select(x => new AssetCategoryDto(
                x.Category.Id, x.Category.Name, x.Category.CodePrefix, x.Category.Method,
                x.Category.UsefulLifeMonths, x.Category.ResidualRate, x.Category.Description,
                x.AssetCount))
            .ToList();
    }

    public async Task<int> Handle(CreateAssetCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = new AssetCategory
        {
            Name = request.Data.Name.Trim(),
            CodePrefix = request.Data.CodePrefix.Trim().ToUpperInvariant(),
            Method = request.Data.Method,
            UsefulLifeMonths = request.Data.UsefulLifeMonths,
            ResidualRate = request.Data.ResidualRate,
            Description = request.Data.Description?.Trim(),
        };

        categories.Add(category);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return category.Id;
    }

    public async Task Handle(UpdateAssetCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await categories.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(AssetCategory), request.Id);

        // Prefix changes only affect future codes; existing asset codes are immutable.
        category.Name = request.Data.Name.Trim();
        category.CodePrefix = request.Data.CodePrefix.Trim().ToUpperInvariant();
        category.Method = request.Data.Method;
        category.UsefulLifeMonths = request.Data.UsefulLifeMonths;
        category.ResidualRate = request.Data.ResidualRate;
        category.Description = request.Data.Description?.Trim();

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task Handle(DeleteAssetCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await categories.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(AssetCategory), request.Id);

        var assetCount = await categories.CountAssetsAsync(request.Id, cancellationToken);
        if (assetCount > 0)
        {
            throw new ConflictException(
                $"Category '{category.Name}' has {assetCount} asset(s) and cannot be deleted.");
        }

        categories.Remove(category); // soft delete via audit interceptor
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
