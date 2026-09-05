namespace Shifts.Api.Features.Shifts.Dtos;

/// <summary>
/// Client-facing claimed shift. Fields match <see cref="ClaimedRow"/> and the
/// take-next <c>OUTPUT</c> columns exactly.
/// </summary>
public sealed class ClaimedShiftResponse
{
    /// <summary>Claimed shift identifier.</summary>
    public int Id { get; init; }

    /// <summary>Business code.</summary>
    public string Code { get; init; } = null!;

    /// <summary>Selection priority.</summary>
    public int Priority { get; init; }

    /// <summary>When the shift was created.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>Owning branch identifier.</summary>
    public int BranchId { get; init; }

    /// <summary>Owning institution identifier.</summary>
    public int InstitutionId { get; init; }
}
