using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shifts.Api.Data;
using Shifts.Api.Domain.Enums;
using Shifts.Api.Features.Shifts.Dtos;

namespace Shifts.Tests;

/// <summary>
/// Concurrency test against real SQL Server locking semantics.
/// Must not use the EF in-memory provider.
/// </summary>
[Collection("Integration")]
public sealed class ShiftsConcurrencyTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ApiFactory _factory;

    /// <summary>
    /// Creates the test class with the shared factory.
    /// </summary>
    /// <param name="factory">Collection fixture hosting the API.</param>
    public ShiftsConcurrencyTests(ApiFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Restores seed data before each test method.
    /// </summary>
    /// <returns>A task that completes when the database is reset.</returns>
    public Task InitializeAsync() => _factory.ResetDatabaseAsync();

    /// <inheritdoc />
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// Parallel take-next calls never claim the same shift.
    /// </summary>
    [Fact]
    public async Task Concurrent_take_requests_never_claim_the_same_shift()
    {
        const int parallelCalls = 3;
        var tasks = Enumerable.Range(0, parallelCalls)
            .Select(_ => ClaimAsAnaAsync())
            .ToArray();

        var claimed = await Task.WhenAll(tasks);
        var ids = claimed.Select(c => c.Id).ToArray();

        Assert.Equal(parallelCalls, ids.Distinct().Count());
        Assert.Equal(ids.OrderBy(id => id), new[] { 1, 2, 3 }.OrderBy(id => id));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rows = await db.Shifts.AsNoTracking()
            .Where(s => ids.Contains(s.Id))
            .ToListAsync();

        Assert.Equal(parallelCalls, rows.Count);
        Assert.All(rows, row =>
        {
            Assert.Equal(ShiftStatus.InProgress, row.Status);
            Assert.Equal(1, row.TakenBy);
            Assert.NotNull(row.TakenAt);
        });
    }

    /// <summary>
    /// Sends one take-next request as Ana (user 1).
    /// </summary>
    /// <returns>The claimed shift.</returns>
    private async Task<ClaimedShiftResponse> ClaimAsAnaAsync()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", "1");
        var response = await client.PostAsync("/api/turnos/tomar-siguiente", content: null);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ClaimedShiftResponse>(JsonOptions);
        Assert.NotNull(body);
        return body;
    }
}
