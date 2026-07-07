using EMS.Application.Common.Exceptions;
using EMS.Application.Common.Security;
using EMS.Domain.Common;
using EMS.Domain.Repositories;
using EMS.Shared.Authorization;
using EMS.Shared.Employees;
using FluentValidation;
using MediatR;

namespace EMS.Application.Designations;

[RequiresPermission(Permissions.Administration.Settings)]
public sealed record UpdateDesignationCommand(int Id, SaveDesignationRequest Data) : IRequest;

internal sealed class UpdateDesignationCommandValidator : AbstractValidator<UpdateDesignationCommand>
{
    public UpdateDesignationCommandValidator(IDesignationRepository designations)
    {
        RuleFor(x => x.Data.Title).NotEmpty().MaximumLength(100).OverridePropertyName("Title");
        RuleFor(x => x.Data.Description).MaximumLength(500).OverridePropertyName("Description");

        RuleFor(x => x)
            .MustAsync(async (cmd, ct) => !await designations.TitleTakenAsync(cmd.Data.Title, cmd.Id, ct))
            .WithMessage("A designation with this title already exists.")
            .OverridePropertyName("Title")
            .When(x => !string.IsNullOrWhiteSpace(x.Data.Title));
    }
}

internal sealed class UpdateDesignationCommandHandler(
    IDesignationRepository designations,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateDesignationCommand>
{
    public async Task Handle(UpdateDesignationCommand request, CancellationToken cancellationToken)
    {
        var designation = await designations.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Designation), request.Id);

        designation.Title = request.Data.Title.Trim();
        designation.Description = request.Data.Description?.Trim();

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
