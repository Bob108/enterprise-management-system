using EMS.Application.Common.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EMS.WebAPI.Middleware;

/// <summary>
/// Maps application exceptions to RFC 7807 responses (design §8): validation → 400 with
/// per-field errors, not found → 404, forbidden → 403, business/concurrency conflict → 409.
/// </summary>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ValidationException ex)
        {
            await WriteProblemAsync(context, new ValidationProblemDetails(
                ex.Errors.ToDictionary(kv => kv.Key, kv => kv.Value))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "One or more validation errors occurred.",
            });
        }
        catch (NotFoundException ex)
        {
            await WriteProblemAsync(context, new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = ex.Message,
            });
        }
        catch (ForbiddenAccessException)
        {
            await WriteProblemAsync(context, new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "You do not have permission to perform this action.",
            });
        }
        catch (UnauthorizedAccessException)
        {
            await WriteProblemAsync(context, new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Authentication is required.",
            });
        }
        catch (ConflictException ex)
        {
            await WriteProblemAsync(context, new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = ex.Message,
            });
        }
        catch (EMS.Domain.Common.DomainException ex)
        {
            // Business-rule violation raised by entity behavior (e.g. illegal status transition).
            await WriteProblemAsync(context, new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = ex.Message,
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            await WriteProblemAsync(context, new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "The record was modified by someone else. Reload and try again.",
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception for {Method} {Path}",
                context.Request.Method, context.Request.Path);
            await WriteProblemAsync(context, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred.",
            });
        }
    }

    private static async Task WriteProblemAsync(HttpContext context, ProblemDetails problem)
    {
        if (context.Response.HasStarted)
        {
            throw new InvalidOperationException("Response already started; cannot write problem details.");
        }

        context.Response.StatusCode = problem.Status!.Value;
        await context.Response.WriteAsJsonAsync(problem, problem.GetType(), options: null,
            contentType: "application/problem+json");
    }
}
