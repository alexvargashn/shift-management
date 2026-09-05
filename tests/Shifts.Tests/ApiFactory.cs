using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shifts.Api.Data;
using Shifts.Api.Infrastructure;

namespace Shifts.Tests;

/// <summary>
/// Hosts the API against <c>ShiftsDb_Tests</c> on the compose SQL Server instance.
/// Overrides the <c>ShiftsDb</c> connection string for both <c>AddDbContext</c>
/// and any startup migrate. Does not rely on Development auto-migrate.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>Dedicated catalog used only by integration tests.</summary>
    public const string TestDatabaseName = "ShiftsDb_Tests";

    private string _testConnectionString = null!;

    /// <summary>
    /// Configures the test host to use <see cref="TestDatabaseName"/> and the
    /// <c>Testing</c> environment so Development startup migrate does not run.
    /// </summary>
    /// <param name="builder">Web host builder.</param>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        RepoEnvLoader.Load();

        var template = "Server=localhost,1433;Database=ShiftsDb;User Id=sa;TrustServerCertificate=True;Encrypt=False";
        _testConnectionString = RepoEnvLoader.ForDatabase(template, TestDatabaseName);

        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:ShiftsDb", _testConnectionString);
        builder.ConfigureServices(services =>
        {
            foreach (var descriptor in services.Where(IsAppDbContextRegistration).ToList())
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(
                    _testConnectionString,
                    sql => sql.EnableRetryOnFailure()));
        });
    }

    /// <summary>
    /// Verifies the resolved context targets <see cref="TestDatabaseName"/> and
    /// creates the schema from migrations.
    /// </summary>
    /// <returns>A task that completes when the test database is ready.</returns>
    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var connectionString = db.Database.GetDbConnection().ConnectionString;

        if (connectionString.IndexOf(TestDatabaseName, StringComparison.OrdinalIgnoreCase) < 0)
        {
            throw new InvalidOperationException("Test factory is not using ShiftsDb_Tests.");
        }

        if (IsExactDatabase(connectionString, "ShiftsDb"))
        {
            throw new InvalidOperationException("Test factory resolved the development ShiftsDb catalog.");
        }

        await ResetDatabaseAsync();
    }

    /// <summary>
    /// Drops and recreates the test database so seed data is restored.
    /// Independent of <c>IsDevelopment()</c> auto-migrate in Program.
    /// </summary>
    /// <returns>A task that completes when migrate has finished.</returns>
    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
    }

    /// <summary>
    /// Disposes the test host after the collection finishes.
    /// </summary>
    /// <returns>A task that completes when the host is disposed.</returns>
    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    /// <summary>
    /// Whether a service descriptor is the EF registration for <see cref="AppDbContext"/>.
    /// </summary>
    /// <param name="descriptor">DI descriptor.</param>
    /// <returns><c>true</c> when the descriptor should be replaced.</returns>
    private static bool IsAppDbContextRegistration(ServiceDescriptor descriptor)
    {
        return descriptor.ServiceType == typeof(AppDbContext)
               || descriptor.ServiceType == typeof(DbContextOptions<AppDbContext>)
               || descriptor.ServiceType == typeof(DbContextOptions);
    }

    /// <summary>
    /// Detects a connection string whose catalog is exactly <paramref name="databaseName"/>,
    /// not a longer name that only starts with it.
    /// </summary>
    /// <param name="connectionString">Raw connection string.</param>
    /// <param name="databaseName">Catalog name to match exactly.</param>
    /// <returns><c>true</c> when the catalog equals <paramref name="databaseName"/>.</returns>
    private static bool IsExactDatabase(string connectionString, string databaseName)
    {
        return Regex.IsMatch(
            connectionString,
            $@"(?:Initial Catalog|Database)\s*=\s*{Regex.Escape(databaseName)}\s*(;|$)",
            RegexOptions.IgnoreCase);
    }
}
