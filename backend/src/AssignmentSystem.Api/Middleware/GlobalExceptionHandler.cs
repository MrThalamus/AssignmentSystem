using AssignmentSystem.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ApplicationException = AssignmentSystem.Application.Common.Exceptions;

namespace AssignmentSystem.Api.Middleware;

/// <summary>
/// Turns the exception types the application throws into RFC 7807 problem details,
/// so every controller can throw rather than repeat status-code plumbing. Anything
/// unrecognised becomes a 500 with a generic message - internal details go to the
/// log, never to the client.
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = Translate(exception, httpContext);

        if (problem.Status >= StatusCodes.Status500InternalServerError)
            _logger.LogError(exception, "Unhandled exception on {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);
        else
            _logger.LogInformation("Request rejected ({Status}) on {Method} {Path}: {Detail}",
                problem.Status, httpContext.Request.Method, httpContext.Request.Path, problem.Detail);

        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);

        return true;
    }

    private static ProblemDetails Translate(Exception exception, HttpContext context)
    {
        var problem = exception switch
        {
            ApplicationException.ValidationException validation => new ValidationProblemDetails(
                validation.Errors.ToDictionary(e => e.Key, e => e.Value))
            {
                Title = "One or more validation errors occurred.",
                Status = StatusCodes.Status400BadRequest
            },

            ApplicationException.NotFoundException => new ProblemDetails
            {
                Title = "Resource not found.",
                Detail = exception.Message,
                Status = StatusCodes.Status404NotFound
            },

            ApplicationException.ForbiddenException => new ProblemDetails
            {
                Title = "Access denied.",
                Detail = exception.Message,
                Status = StatusCodes.Status403Forbidden
            },

            ApplicationException.ConflictException => new ProblemDetails
            {
                Title = "Request conflicts with the current state.",
                Detail = exception.Message,
                Status = StatusCodes.Status409Conflict
            },

            // A rule the domain refused: the request was well-formed but not allowed
            // for the data as it stands.
            BusinessRuleViolationException => new ProblemDetails
            {
                Title = "The operation is not allowed.",
                Detail = exception.Message,
                Status = StatusCodes.Status409Conflict
            },

            _ => new ProblemDetails
            {
                Title = "An unexpected error occurred.",
                Detail = "Please try again. If the problem persists, contact an administrator.",
                Status = StatusCodes.Status500InternalServerError
            }
        };

        problem.Instance = context.Request.Path;
        problem.Extensions["traceId"] = context.TraceIdentifier;

        return problem;
    }
}
