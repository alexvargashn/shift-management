namespace Shifts.Tests;

/// <summary>
/// Serializes integration test classes that share <see cref="ApiFactory"/>
/// and the same <c>ShiftsDb_Tests</c> catalog. Prevents parallel
/// EnsureDeleted/Migrate from destroying the database mid-run.
/// </summary>
[CollectionDefinition("Integration")]
public sealed class IntegrationCollection : ICollectionFixture<ApiFactory>
{
}
