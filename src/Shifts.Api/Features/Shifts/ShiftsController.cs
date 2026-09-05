using Microsoft.AspNetCore.Mvc;
using Shifts.Api.Errors;
using Shifts.Api.Features.Shifts.Dtos;

namespace Shifts.Api.Features.Shifts;

/// <summary>
/// Thin HTTP adapter for the three required shift operations.
/// Routes stay in Spanish; identifiers stay in English.
/// Write actions accept no request body so the client cannot smuggle scope.
/// </summary>
[ApiController]
[Route("api/turnos")]
public sealed class ShiftsController : ControllerBase
{
    private readonly IShiftService _shiftService;

    /// <summary>
    /// Creates the controller.
    /// </summary>
    /// <param name="shiftService">Application service that owns business rules.</param>
    public ShiftsController(IShiftService shiftService)
    {
        _shiftService = shiftService;
    }

    /// <summary>
    /// Lists pending shifts inside the caller's institution and authorized branches.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Pending shifts, or an empty list when the user has no authorized branches.</returns>
    [HttpGet("pendientes")]
    [ProducesResponseType(typeof(IReadOnlyList<PendingShiftResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<PendingShiftResponse>>> GetPending(CancellationToken cancellationToken)
    {
        var items = await _shiftService.GetPendingAsync(cancellationToken);
        return Ok(items);
    }

    /// <summary>
    /// Claims the next pending shift using the deterministic, concurrency-safe rule.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The claimed shift, or 404 when none are available.</returns>
    [HttpPost("tomar-siguiente")]
    [ProducesResponseType(typeof(ClaimedShiftResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TakeNext(CancellationToken cancellationToken)
    {
        var result = await _shiftService.TakeNextAsync(cancellationToken);
        return Map(result, success => Ok(success), "No pending shift is available in scope.");
    }

    /// <summary>
    /// Finishes an in-progress shift identified only by the route id.
    /// </summary>
    /// <param name="id">Shift identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 on success; 400/403/404/409 on known failures.</returns>
    [HttpPost("{id:int}/finalizar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Finish(int id, CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: "Shift id must be a positive integer.");
        }

        var result = await _shiftService.FinishAsync(id, cancellationToken);
        return Map(result, _ => Ok(), "The shift was not found.");
    }

    /// <summary>
    /// Maps a service result to an HTTP response without embedding domain rules.
    /// </summary>
    /// <typeparam name="T">Success payload type.</typeparam>
    /// <param name="result">Service result.</param>
    /// <param name="onSuccess">HTTP result factory for success.</param>
    /// <param name="notFoundDetail">Client-safe 404 detail.</param>
    /// <returns>The mapped action result.</returns>
    private IActionResult Map<T>(
        ShiftResult<T> result,
        Func<T?, IActionResult> onSuccess,
        string notFoundDetail)
    {
        return result.Outcome switch
        {
            ShiftOutcome.Success => onSuccess(result.Value),
            ShiftOutcome.NoneAvailable => Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found",
                detail: notFoundDetail),
            ShiftOutcome.NotFound => Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found",
                detail: notFoundDetail),
            ShiftOutcome.Forbidden => Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Forbidden",
                detail: "The requested operation is outside the authorized scope."),
            ShiftOutcome.Conflict => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Conflict",
                detail: "The shift cannot change to the requested state."),
            _ => Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Unexpected error",
                detail: "An unexpected error occurred.")
        };
    }
}
