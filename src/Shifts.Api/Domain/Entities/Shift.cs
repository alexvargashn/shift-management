using Shifts.Api.Domain.Enums;

namespace Shifts.Api.Domain.Entities;

/// <summary>
/// A shift (turno) belongs to an institution and a branch. Lifecycle:
/// Pending -> InProgress -> Completed. <see cref="TakenBy"/>/<see cref="TakenAt"/>
/// are set when an operator claims it; <see cref="CompletedAt"/> when finished.
/// </summary>
public class Shift
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }

    /// <summary>Owning institution (tenant). Derived server-side, never from the client.</summary>
    public int InstitutionId { get; set; }

    /// <summary>Owning branch (sucursal). Must be inside the caller's authorized scope.</summary>
    public int BranchId { get; set; }

    /// <summary>Human-readable shift code (e.g. A-001).</summary>
    public string Code { get; set; } = null!;

    /// <summary>Higher values are selected first by take-next.</summary>
    public int Priority { get; set; }

    /// <summary>Creation timestamp. Older pending shifts win when priority is equal.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Current lifecycle state. Only Pending, InProgress, and Completed are valid.</summary>
    public ShiftStatus Status { get; set; } = ShiftStatus.Pending;

    /// <summary>User id that claimed the shift. Null while pending.</summary>
    public int? TakenBy { get; set; }

    /// <summary>When the shift was claimed. Null while pending.</summary>
    public DateTime? TakenAt { get; set; }

    /// <summary>When the shift was finished. Null until completed.</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>Navigation to the owning institution.</summary>
    public Institution Institution { get; set; } = null!;

    /// <summary>Navigation to the owning branch.</summary>
    public Branch Branch { get; set; } = null!;

    /// <summary>Navigation to the user who claimed the shift, if any.</summary>
    public User? TakenByUser { get; set; }
}
