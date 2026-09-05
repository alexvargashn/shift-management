namespace Shifts.Api.Errors;

/// <summary>
/// Explicit result for a shift write so the controller can map HTTP status
/// without putting business rules in the action.
/// </summary>
/// <typeparam name="T">Payload type on success.</typeparam>
public sealed class ShiftResult<T>
{
    private ShiftResult(ShiftOutcome outcome, T? value)
    {
        Outcome = outcome;
        Value = value;
    }

    /// <summary>Business outcome used for HTTP mapping.</summary>
    public ShiftOutcome Outcome { get; }

    /// <summary>Success payload; null when the outcome is not <see cref="ShiftOutcome.Success"/>.</summary>
    public T? Value { get; }

    /// <summary>Whether the operation succeeded.</summary>
    public bool IsSuccess => Outcome == ShiftOutcome.Success;

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <param name="value">Payload to return to the client.</param>
    /// <returns>A success result wrapping <paramref name="value"/>.</returns>
    public static ShiftResult<T> Ok(T value) => new(ShiftOutcome.Success, value);

    /// <summary>
    /// Creates a failed result with no payload.
    /// </summary>
    /// <param name="outcome">Non-success outcome.</param>
    /// <returns>A failure result.</returns>
    public static ShiftResult<T> Fail(ShiftOutcome outcome) => new(outcome, default);
}
