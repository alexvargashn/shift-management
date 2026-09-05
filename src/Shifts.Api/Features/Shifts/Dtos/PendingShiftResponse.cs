namespace Shifts.Api.Features.Shifts.Dtos;

/// <summary>
/// Client-facing pending shift. Does not expose navigation graphs or
/// client-writable tenant fields.
/// </summary>
public sealed class PendingShiftResponse
{
    /// <summary>Shift identifier.</summary>
    public int Id { get; init; }

    /// <summary>Business code.</summary>
    public string Code { get; init; } = null!;

    /// <summary>Selection priority (higher is claimed first).</summary>
    public int Priority { get; init; }

    /// <summary>When the shift was created.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>Owning branch identifier.</summary>
    public int BranchId { get; init; }
}
