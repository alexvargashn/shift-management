using DotNetEnv;
using Microsoft.Data.SqlClient;

namespace Shifts.Api.Infrastructure;

/// <summary>
/// Loads the repository-root <c>.env</c> with an explicit path and applies
/// <c>MSSQL_SA_PASSWORD</c> to the existing <c>ShiftsDb</c> connection string.
/// </summary>
public static class RepoEnvLoader
{
    private const string PasswordVariableName = "MSSQL_SA_PASSWORD";

    /// <summary>
    /// Walks up from <see cref="AppContext.BaseDirectory"/> until a
    /// <c>.sln</c> or <c>.git</c> marker is found, then loads that folder's
    /// <c>.env</c> via DotNetEnv. Fails fast if the file or password is missing.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the repository root, <c>.env</c> file, or
    /// <c>MSSQL_SA_PASSWORD</c> cannot be resolved.
    /// </exception>
    public static void Load()
    {
        var repoRoot = FindRepoRoot();
        if (repoRoot is null)
        {
            throw new InvalidOperationException(
                "Could not locate the repository root (.sln or .git) to load .env.");
        }

        var envPath = Path.Combine(repoRoot, ".env");
        if (!File.Exists(envPath))
        {
            throw new InvalidOperationException(
                $"Missing '{envPath}'. The proof .env must exist at the repository root.");
        }

        Env.Load(envPath);

        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(PasswordVariableName)))
        {
            throw new InvalidOperationException(
                $"{PasswordVariableName} is not set after loading '{envPath}'.");
        }
    }

    /// <summary>
    /// Copies <c>MSSQL_SA_PASSWORD</c> into the given SQL Server connection string.
    /// Host, database, and user remain those already configured for <c>ShiftsDb</c>.
    /// </summary>
    /// <param name="connectionString">Connection string without a production secret.</param>
    /// <returns>The same connection string with the process password applied.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="connectionString"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the password is not in the environment.</exception>
    public static string ApplyPassword(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string is required.", nameof(connectionString));
        }

        var password = Environment.GetEnvironmentVariable(PasswordVariableName);
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                $"{PasswordVariableName} is not set. Load the repository-root .env first.");
        }

        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            Password = password
        };
        return builder.ConnectionString;
    }

    /// <summary>
    /// Builds a connection string for a specific database name using the same
    /// host, user, and password as <paramref name="connectionString"/>.
    /// </summary>
    /// <param name="connectionString">Base <c>ShiftsDb</c> connection string.</param>
    /// <param name="databaseName">Catalog to target (for example <c>ShiftsDb_Tests</c>).</param>
    /// <returns>A connection string pointing at <paramref name="databaseName"/>.</returns>
    public static string ForDatabase(string connectionString, string databaseName)
    {
        var builder = new SqlConnectionStringBuilder(ApplyPassword(connectionString))
        {
            InitialCatalog = databaseName
        };
        return builder.ConnectionString;
    }

    /// <summary>
    /// Walks parent directories from the application base directory until a
    /// solution file or git metadata folder is found.
    /// </summary>
    /// <returns>The repository root path, or <c>null</c> if none is found.</returns>
    public static string? FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var hasSolution = current.GetFiles("*.sln").Length > 0;
            var hasGit = Directory.Exists(Path.Combine(current.FullName, ".git"));
            if (hasSolution || hasGit)
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }
}
