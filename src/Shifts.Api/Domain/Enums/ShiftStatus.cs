namespace Shifts.Api.Domain.Enums;

/// <summary>
/// Lifecycle states of a shift. Persisted as <c>int</c>.
/// Values are assigned explicitly because the filtered index used by the
/// "take next pending shift" flow references Pending (0) directly in its
/// SQL filter, so the numeric contract must stay stable.
/// </summary>
public enum ShiftStatus
{
    Pending = 0,
    InProgress = 1,
    Completed = 2
}