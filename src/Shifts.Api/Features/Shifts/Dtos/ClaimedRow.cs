namespace Shifts.Api.Features.Shifts.Dtos;

/// <summary>
/// Result shape for the take-next <c>OUTPUT</c> clause. Property names must
/// match the SQL column names exactly so <c>SqlQueryRaw</c> can map the row.
/// </summary>
public sealed class ClaimedRow
{
    /// <summary>Claimed shift identifier.</summary>
    public int Id { get; set; }

    /// <summary>Business code of the claimed shift.</summary>
    public string Code { get; set; } = null!;

    /// <summary>Priority used for deterministic selection.</summary>
    public int Priority { get; set; }

    /// <summary>Creation timestamp of the claimed shift.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Owning branch identifier.</summary>
    public int BranchId { get; set; }

    /// <summary>Owning institution identifier.</summary>
    public int InstitutionId { get; set; }
}
