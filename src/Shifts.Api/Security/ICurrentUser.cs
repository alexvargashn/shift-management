namespace Shifts.Api.Security;

/// <summary>
/// Server-derived identity and authorization scope for the current request.
/// Values come from the database after <c>X-User-Id</c> is resolved; they are
/// never read from the client body, query, or extra headers.
/// </summary>
public interface ICurrentUser
{
    /// <summary>Persisted user identifier.</summary>
    int Id { get; }

    /// <summary>Institution (tenant) loaded from the user row.</summary>
    int InstitutionId { get; }

    /// <summary>Branch identifiers authorized via <c>UserBranch</c>.</summary>
    IReadOnlyList<int> AuthorizedBranchIds { get; }
}
