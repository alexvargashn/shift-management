using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Shifts.Api.Errors;

/// <summary>
/// Converts unhandled exceptions into a generic ProblemDetails 500 response.
/// Never writes stack traces, connection strings, or provider messages.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    /// <summary>
    /// Creates the handler.
    /// </summary>
    /// <param name="logger">Logger used for server-side diagnosis only.</param>
    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Logs the unexpected failure without leaking internals to the client.
    /// </summary>
    /// <param name="httpContext">Current HTTP context.</param>
    /// <param name="exception">Unhandled exception.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> after a generic 500 is written.</returns>
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError("Unhandled exception. ExceptionType={ExceptionType}", exception.GetType().Name);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Unexpected error",
            Detail = "An unexpected error occurred."
        }, cancellationToken);

        return true;
    }
}
