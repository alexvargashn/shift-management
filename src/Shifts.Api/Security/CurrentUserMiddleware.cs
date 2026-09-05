using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shifts.Api.Data;

namespace Shifts.Api.Security;

/// <summary>
/// Resolves <c>X-User-Id</c> for <c>/api</c> requests and writes the scoped
/// <see cref="CurrentUser"/>. The middleware is a singleton and must not
/// constructor-inject the scoped user.
/// </summary>
public sealed class CurrentUserMiddleware
{
    private const string UserIdHeader = "X-User-Id";
    private readonly RequestDelegate _next;

    /// <summary>
    /// Creates the middleware. Only singleton-safe services belong here.
    /// </summary>
    /// <param name="next">The next delegate in the pipeline.</param>
    public CurrentUserMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Identifies the caller on <c>/api</c> routes and populates
    /// <see cref="CurrentUser"/> from the database.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <returns>A task that completes when the pipeline continues or a 401 is written.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(UserIdHeader, out var headerValues)
            || !int.TryParse(headerValues.FirstOrDefault(), out var userId))
        {
            await WriteUnauthorizedAsync(context);
            return;
        }

        var db = context.RequestServices.GetRequiredService<AppDbContext>();
        var user = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                u.Id,
                u.InstitutionId,
                BranchIds = u.AuthorizedBranches.Select(ab => ab.BranchId).ToList()
            })
            .FirstOrDefaultAsync(context.RequestAborted);

        if (user is null)
        {
            await WriteUnauthorizedAsync(context);
            return;
        }

        var currentUser = context.RequestServices.GetRequiredService<CurrentUser>();
        currentUser.Id = user.Id;
        currentUser.InstitutionId = user.InstitutionId;
        currentUser.AuthorizedBranchIds = user.BranchIds;

        await _next(context);
    }

    /// <summary>
    /// Writes a client-safe 401 ProblemDetails payload without leaking internals.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    private static async Task WriteUnauthorizedAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "The user could not be identified."
            },
            options: null,
            contentType: "application/problem+json");
    }
}
