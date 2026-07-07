using EMS.Application.Common.Security;
using EMS.Domain.Common;
using EMS.Domain.Entities;
using EMS.Domain.Repositories;
using EMS.Shared.Authorization;
using EMS.Shared.Employees;
using FluentValidation;
using MediatR;

namespace EMS.Application.Designations;

[RequiresPermission(Permissions.Administration.Settings)]
public sealed record CreateDesignationCommand(SaveDesignationRequest Data) : IRequest<int>;

internal sealed class CreateDesignationCommandValidator : AbstractValidator<CreateDesignationCommand>
{
    public CreateDesignationCommandValidator(IDesignationRepository designations)
    {
        RuleFor(x => x.Data.Title).NotEmpty().MaximumLength(100).OverridePropertyName("Title");
        RuleFor(x => x.Data.Description).MaximumLength(500).OverridePropertyName("Description");

        RuleFor(x => x.Data.Title)
            .MustAsync(async (title, ct) => !await designations.TitleTakenAsync(title, null, ct))
            .WithMessage("A designation with this title already exists.")
            .OverridePropertyName("Title")
            .When(x => !string.IsNullOrWhiteSpace(x.Data.Title));
    }
}

internal sealed class CreateDesignationCommandHandler(
    IDesignationRepository designations,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateDesignationCommand, int>
{
    public async Task<int> Handle(CreateDesignationCommand request, CancellationToken cancellationToken)
    {
        var designation = new Designation
        {
            Title = request.Data.Title.Trim(),
            Description = request.Data.Description?.Trim(),
        };

        designations.Add(designation);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return designation.Id;
    }
}
