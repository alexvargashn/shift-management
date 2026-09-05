using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shifts.Api.Data;
using Shifts.Api.Domain.Enums;
using Shifts.Api.Features.Shifts.Dtos;

namespace Shifts.Tests;

/// <summary>
/// Mandatory HTTP contract tests against real SQL Server.
/// </summary>
[Collection("Integration")]
public sealed class ShiftsApiTests : IAsyncLifetime
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
    public ShiftsApiTests(ApiFactory factory)
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
    /// A user can list only pending shifts inside their authorized scope.
    /// </summary>
    [Fact]
    public async Task User_lists_only_shifts_within_scope()
    {
        var ana = await GetPendingAsync(userId: 1);
        var beto = await GetPendingAsync(userId: 2);
        var carla = await GetPendingAsync(userId: 3);

        Assert.Equal(new[] { 3, 2, 1 }, ana.Select(s => s.Id).ToArray());
        Assert.Equal(new[] { 3, 2, 4, 1 }, beto.Select(s => s.Id).ToArray());
        Assert.Equal(new[] { 5 }, carla.Select(s => s.Id).ToArray());
        Assert.DoesNotContain(ana, s => s.Id is 4 or 5);
    }

    /// <summary>
    /// A user cannot operate on another institution's data.
    /// </summary>
    [Fact]
    public async Task User_cannot_operate_on_another_institution()
    {
        using var ana = CreateClient(1);
        using var carla = CreateClient(3);

        var finishForeign = await ana.PostAsync("/api/turnos/5/finalizar", content: null);
        Assert.Equal(HttpStatusCode.Forbidden, finishForeign.StatusCode);

        var carlaPending = await GetPendingAsync(3);
        Assert.DoesNotContain(carlaPending, s => s.Id is 1 or 2 or 3 or 4);

        var claimed = await carla.PostAsync("/api/turnos/tomar-siguiente", content: null);
        claimed.EnsureSuccessStatusCode();
        var body = await claimed.Content.ReadFromJsonAsync<ClaimedShiftResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(5, body.Id);
        Assert.Equal(2, body.InstitutionId);
    }

    /// <summary>
    /// Take-next follows Priority DESC, CreatedAt ASC, Id ASC on branch 1.
    /// Seed order is Id 3, then 2, then 1.
    /// </summary>
    [Fact]
    public async Task Take_next_respects_priority_and_age_order()
    {
        using var ana = CreateClient(1);

        var first = await ClaimAsync(ana);
        var second = await ClaimAsync(ana);
        var third = await ClaimAsync(ana);

        Assert.Equal(3, first.Id);
        Assert.Equal(2, second.Id);
        Assert.Equal(1, third.Id);
    }

    /// <summary>
    /// A finished shift cannot be claimed again.
    /// </summary>
    [Fact]
    public async Task Finished_shift_cannot_be_taken_again()
    {
        using var ana = CreateClient(1);
        var claimed = await ClaimAsync(ana);

        var finish = await ana.PostAsync($"/api/turnos/{claimed.Id}/finalizar", content: null);
        Assert.Equal(HttpStatusCode.OK, finish.StatusCode);

        var next = await ClaimAsync(ana);
        Assert.NotEqual(claimed.Id, next.Id);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var finished = await db.Shifts.AsNoTracking().SingleAsync(s => s.Id == claimed.Id);
        Assert.Equal(ShiftStatus.Completed, finished.Status);
    }

    /// <summary>
    /// Finishing a pending shift is an invalid transition and returns 409.
    /// </summary>
    [Fact]
    public async Task Invalid_transition_returns_conflict()
    {
        using var ana = CreateClient(1);
        var response = await ana.PostAsync("/api/turnos/1/finalizar", content: null);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    /// <summary>
    /// Missing <c>X-User-Id</c> is treated as unidentified.
    /// </summary>
    [Fact]
    public async Task Missing_user_header_returns_unauthorized()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/turnos/pendientes");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Creates an HTTP client that sends the simulated identity header.
    /// </summary>
    /// <param name="userId">Persisted user id.</param>
    /// <returns>A configured client.</returns>
    private HttpClient CreateClient(int userId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", userId.ToString());
        return client;
    }

    /// <summary>
    /// Lists pending shifts for a user.
    /// </summary>
    /// <param name="userId">Persisted user id.</param>
    /// <returns>The pending list.</returns>
    private async Task<List<PendingShiftResponse>> GetPendingAsync(int userId)
    {
        using var client = CreateClient(userId);
        var response = await client.GetAsync("/api/turnos/pendientes");
        response.EnsureSuccessStatusCode();
        var items = await response.Content.ReadFromJsonAsync<List<PendingShiftResponse>>(JsonOptions);
        Assert.NotNull(items);
        return items;
    }

    /// <summary>
    /// Claims the next shift and asserts success.
    /// </summary>
    /// <param name="client">Authenticated client.</param>
    /// <returns>The claimed shift.</returns>
    private static async Task<ClaimedShiftResponse> ClaimAsync(HttpClient client)
    {
        var response = await client.PostAsync("/api/turnos/tomar-siguiente", content: null);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ClaimedShiftResponse>(JsonOptions);
        Assert.NotNull(body);
        return body;
    }
}
