using System.Reflection;
using EMS.Application.Common.Exceptions;
using EMS.Application.Common.Interfaces;
using EMS.Application.Common.Security;
using MediatR;

namespace EMS.Application.Common.Behaviors;

/// <summary>
/// Enforces [RequiresPermission] on commands/queries. Second line of defense behind the
/// API's endpoint-level [HasPermission] checks (design §9.2).
/// </summary>
public sealed class AuthorizationBehavior<TRequest, TResponse>(ICurrentUser currentUser)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requirements = request.GetType()
            .GetCustomAttributes<RequiresPermissionAttribute>(inherit: true)
            .ToArray();

        if (requirements.Length > 0)
        {
            if (!currentUser.IsAuthenticated)
            {
                throw new UnauthorizedAccessException();
            }

            foreach (var requirement in requirements)
            {
                if (!currentUser.HasPermission(requirement.Permission))
                {
                    throw new ForbiddenAccessException(requirement.Permission);
                }
            }
        }

        return next();
    }
}
