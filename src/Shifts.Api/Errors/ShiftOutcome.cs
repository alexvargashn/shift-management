namespace Shifts.Api.Errors;

/// <summary>
/// Business outcome of a shift write. The controller maps these values to HTTP
/// status codes; unexpected failures stay as unhandled exceptions (500).
/// </summary>
public enum ShiftOutcome
{
    /// <summary>The operation completed successfully.</summary>
    Success = 0,

    /// <summary>No pending shift exists inside the caller's scope.</summary>
    NoneAvailable = 1,

    /// <summary>The target shift does not exist.</summary>
    NotFound = 2,

    /// <summary>The caller has no scope or the target is out of scope.</summary>
    Forbidden = 3,

    /// <summary>The target exists in scope but is in the wrong state.</summary>
    Conflict = 4
}
