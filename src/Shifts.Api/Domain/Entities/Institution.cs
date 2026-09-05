namespace Shifts.Api.Domain.Entities;

/// <summary>
/// Top-level tenant. Every branch, user and shift belongs to exactly one institution.
/// </summary>
public class Institution
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;

    public ICollection<Branch> Branches { get; set; } = new List<Branch>();
    public ICollection<User> Users { get; set; } = new List<User>();
}