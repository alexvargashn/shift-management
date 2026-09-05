using Shifts.Api.Errors;
using Shifts.Api.Features.Shifts.Dtos;

namespace Shifts.Api.Features.Shifts;

/// <summary>
/// Application service for listing, claiming, and finishing shifts inside
/// the caller's server-derived institution and branch scope.
/// </summary>
public interface IShiftService
{
    /// <summary>
    /// Lists pending shifts visible to the current user, ordered by the
    /// deterministic selection rule. Returns an empty list when the user
    /// has no authorized branches.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Pending shifts in scope.</returns>
    Task<IReadOnlyList<PendingShiftResponse>> GetPendingAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Claims the next pending shift with a single atomic pessimistic SQL statement.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The claimed shift, <see cref="ShiftOutcome.NoneAvailable"/>, or
    /// <see cref="ShiftOutcome.Forbidden"/> when the user has no branches.
    /// </returns>
    Task<ShiftResult<ClaimedShiftResponse>> TakeNextAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Finishes an in-progress shift that is inside the caller's scope.
    /// </summary>
    /// <param name="id">Shift identifier from the route.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success, not found, forbidden, or conflict.</returns>
    Task<ShiftResult<object?>> FinishAsync(int id, CancellationToken cancellationToken);
}
