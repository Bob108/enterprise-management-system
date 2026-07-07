using System.Diagnostics;
using EMS.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EMS.Application.Common.Behaviors;

public sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<TRequest> logger,
    ICurrentUser currentUser) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var response = await next();
            logger.LogInformation(
                "{RequestName} handled in {ElapsedMs} ms for user {UserId}",
                requestName, stopwatch.ElapsedMilliseconds, currentUser.UserId);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "{RequestName} failed after {ElapsedMs} ms for user {UserId}",
                requestName, stopwatch.ElapsedMilliseconds, currentUser.UserId);
            throw;
        }
    }
}
