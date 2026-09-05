namespace Shifts.Api.Domain.Entities;

/// <summary>
/// Join entity mapping which branches a user is authorized to operate in.
/// Modeled explicitly (instead of an implicit EF many-to-many) so the
/// authorization scope can be seeded deterministically and queried directly
/// when deriving a user's scope server-side.
/// </summary>
public class UserBranch
{
    public int UserId { get; set; }
    public int BranchId { get; set; }

    public User User { get; set; } = null!;
    public Branch Branch { get; set; } = null!;
}