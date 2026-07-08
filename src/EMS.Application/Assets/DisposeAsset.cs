using EMS.Application.Common.Exceptions;
using EMS.Application.Common.Interfaces;
using EMS.Application.Common.Security;
using EMS.Domain.Common;
using EMS.Domain.Repositories;
using EMS.Shared.Assets;
using EMS.Shared.Authorization;
using FluentValidation;
using MediatR;

namespace EMS.Application.Assets;

[RequiresPermission(Permissions.Assets.Dispose)]
public sealed record DisposeAssetCommand(int Id, DisposeAssetRequest Data) : IRequest;

internal sealed class DisposeAssetCommandValidator : AbstractValidator<DisposeAssetCommand>
{
    public DisposeAssetCommandValidator()
    {
        RuleFor(x => x.Data.Method).IsInEnum().OverridePropertyName("Method");
        RuleFor(x => x.Data.Proceeds)
            .GreaterThanOrEqualTo(0).OverridePropertyName("Proceeds")
            .When(x => x.Data.Proceeds.HasValue);
        RuleFor(x => x.Data.Reason).MaximumLength(500).OverridePropertyName("Reason");
    }
}

internal sealed class DisposeAssetCommandHandler(
    IAssetRepository assets,
    IUnitOfWork unitOfWork,
    IDateTime clock) : IRequestHandler<DisposeAssetCommand>
{
    public async Task Handle(DisposeAssetCommand request, CancellationToken cancellationToken)
    {
        var asset = await assets.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Asset), request.Id);

        // Gain/loss is computed against book value at the moment of disposal (design §6.3).
        var depreciated = await assets.GetDepreciatedTotalAsync(asset.Id, cancellationToken);
        var bookValue = asset.PurchaseCost - depreciated;

        asset.Dispose(
            request.Data.Method,
            request.Data.Proceeds,
            bookValue,
            request.Data.Reason?.Trim(),
            DateOnly.FromDateTime(clock.UtcNow));

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
