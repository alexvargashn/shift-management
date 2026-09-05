namespace Shifts.Api.Domain.Entities;

/// <summary>
/// A branch (sucursal) always belongs to a single institution.
/// </summary>
public class Branch
{
    public int Id { get; set; }
    public int InstitutionId { get; set; }
    public string Name { get; set; } = null!;

    public Institution Institution { get; set; } = null!;
    public ICollection<UserBranch> AuthorizedUsers { get; set; } = new List<UserBranch>();
}