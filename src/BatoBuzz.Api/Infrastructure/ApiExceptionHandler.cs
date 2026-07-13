using BatoBuzz.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace BatoBuzz.Api.Infrastructure;

public sealed class ApiExceptionHandler : IExceptionHandler
{
    private readonly ILogger<ApiExceptionHandler> _logger;

    public ApiExceptionHandler(ILogger<ApiExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            DomainException => (StatusCodes.Status400BadRequest, "Business rule validation failed", exception.Message),
            ArgumentException => (StatusCodes.Status400BadRequest, "Request validation failed", exception.Message),
            UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "Access denied", exception.Message),
            InvalidOperationException => (StatusCodes.Status400BadRequest, "The operation could not be completed", exception.Message),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Resource not found", exception.Message),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "The record was changed by another operation", "Reload the record and try again."),
            DbUpdateException => (StatusCodes.Status409Conflict, "The change conflicts with existing data", "Review the submitted values and try again."),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred", "The server could not complete the request.")
        };

        if (status >= StatusCodes.Status500InternalServerError)
            _logger.LogError(exception, "Unhandled API exception for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        else
            _logger.LogWarning(exception, "API request rejected for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };
        problem.Extensions["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
