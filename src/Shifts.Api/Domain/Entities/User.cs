namespace Shifts.Api.Domain.Entities;

/// <summary>
/// An operator/supervisor. Belongs to one institution and is authorized to
/// operate in one or more branches within that same institution.
/// </summary>
public class User
{
    public int Id { get; set; }
    public int InstitutionId { get; set; }
    public string Name { get; set; } = null!;
    public string Role { get; set; } = null!;

    public Institution Institution { get; set; } = null!;
    public ICollection<UserBranch> AuthorizedBranches { get; set; } = new List<UserBranch>();
}