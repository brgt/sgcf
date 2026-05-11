using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Sgcf.Api.Middleware;

internal sealed partial class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception: {Message}")]
    private static partial void LogUnhandledException(ILogger logger, string message, Exception exception);

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        LogUnhandledException(logger, exception.Message, exception);

        if (exception is ValidationException validationException)
        {
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

            Dictionary<string, string[]> errors = validationException.Errors
                .GroupBy(f => f.PropertyName, StringComparer.Ordinal)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(f => f.ErrorMessage).ToArray(),
                    StringComparer.Ordinal);

            ValidationProblemDetails problemDetails = new(errors)
            {
                Type = "https://tools.ietf.org/html/rfc7807",
                Title = "Validation failed",
                Status = StatusCodes.Status400BadRequest,
            };

            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
            return true;
        }

        if (exception is KeyNotFoundException)
        {
            httpContext.Response.StatusCode = StatusCodes.Status404NotFound;

            ProblemDetails problemDetails = new()
            {
                Type = "https://tools.ietf.org/html/rfc7807",
                Title = "Resource not found",
                Status = StatusCodes.Status404NotFound,
                Detail = exception.Message,
            };

            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
            return true;
        }

        if (exception is ArgumentException)
        {
            httpContext.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;

            ProblemDetails problemDetails = new()
            {
                Type = "https://tools.ietf.org/html/rfc7807",
                Title = "Unprocessable entity",
                Status = StatusCodes.Status422UnprocessableEntity,
                Detail = exception.Message,
            };

            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
            return true;
        }

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        ProblemDetails internalProblemDetails = new()
        {
            Type = "https://tools.ietf.org/html/rfc7807",
            Title = "An unexpected error occurred",
            Status = StatusCodes.Status500InternalServerError,
        };

        await httpContext.Response.WriteAsJsonAsync(internalProblemDetails, cancellationToken);
        return true;
    }
}
