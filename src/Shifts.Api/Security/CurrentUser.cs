namespace Shifts.Api.Security;

/// <summary>
/// Mutable scoped holder for the request's server-derived user scope.
/// Middleware writes this instance; services read it through <see cref="ICurrentUser"/>.
/// Both resolutions must share this same object per request.
/// </summary>
public sealed class CurrentUser : ICurrentUser
{
    /// <inheritdoc />
    public int Id { get; set; }

    /// <inheritdoc />
    public int InstitutionId { get; set; }

    /// <inheritdoc />
    public IReadOnlyList<int> AuthorizedBranchIds { get; set; } = Array.Empty<int>();
}
