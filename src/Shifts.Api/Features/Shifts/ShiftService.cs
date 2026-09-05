using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Shifts.Api.Data;
using Shifts.Api.Domain.Enums;
using Shifts.Api.Errors;
using Shifts.Api.Features.Shifts.Dtos;
using Shifts.Api.Security;

namespace Shifts.Api.Features.Shifts;

/// <summary>
/// Shift use cases. Tenant scope always comes from <see cref="ICurrentUser"/>.
/// Take-next uses <c>SqlQueryRaw</c> to keep the <c>OUTPUT</c> row; finish uses
/// <c>ExecuteSqlRaw</c> because only the affected-row count is required.
/// </summary>
public sealed class ShiftService : IShiftService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<ShiftService> _logger;

    /// <summary>
    /// Creates the service.
    /// </summary>
    /// <param name="db">EF context used for LINQ reads and parameterized SQL.</param>
    /// <param name="currentUser">Server-derived request identity.</param>
    /// <param name="logger">Diagnostic logger (no secrets).</param>
    public ShiftService(
        AppDbContext db,
        ICurrentUser currentUser,
        ILogger<ShiftService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PendingShiftResponse>> GetPendingAsync(CancellationToken cancellationToken)
    {
        if (_currentUser.AuthorizedBranchIds.Count == 0)
        {
            _logger.LogInformation("Listed pending shifts for user {UserId}, count {Count}", _currentUser.Id, 0);
            return Array.Empty<PendingShiftResponse>();
        }

        var items = await _db.Shifts
            .AsNoTracking()
            .Where(s =>
                s.Status == ShiftStatus.Pending
                && s.InstitutionId == _currentUser.InstitutionId
                && _currentUser.AuthorizedBranchIds.Contains(s.BranchId))
            .OrderByDescending(s => s.Priority)
            .ThenBy(s => s.CreatedAt)
            .ThenBy(s => s.Id)
            .Select(s => new PendingShiftResponse
            {
                Id = s.Id,
                Code = s.Code,
                Priority = s.Priority,
                CreatedAt = s.CreatedAt,
                BranchId = s.BranchId
            })
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Listed pending shifts for user {UserId}, count {Count}", _currentUser.Id, items.Count);
        return items;
    }

    /// <inheritdoc />
    public async Task<ShiftResult<ClaimedShiftResponse>> TakeNextAsync(CancellationToken cancellationToken)
    {
        if (_currentUser.AuthorizedBranchIds.Count == 0)
        {
            _logger.LogWarning("Take-next forbidden for user {UserId}: no authorized branches", _currentUser.Id);
            return ShiftResult<ClaimedShiftResponse>.Fail(ShiftOutcome.Forbidden);
        }

        var parameters = new List<SqlParameter>
        {
            IntParameter("inProgress", (int)ShiftStatus.InProgress),
            IntParameter("institutionId", _currentUser.InstitutionId),
            IntParameter("userId", _currentUser.Id)
        };

        var branchPlaceholders = AddBranchParameters(parameters, _currentUser.AuthorizedBranchIds);

        // Status = 0 is a literal on purpose (ShiftStatus.Pending). A parameter
        // such as Status = @pending cannot match the filtered index
        // IX_Shifts_PendingSelection ([Status] = 0). Without that seek the
        // plan becomes scan + sort, UPDLOCK covers every pending row, and
        // concurrent READPAST callers see an empty queue.
        // READCOMMITTEDLOCK: if the database has READ_COMMITTED_SNAPSHOT enabled
        // (off by default locally, on by default on Azure SQL), reads would be served
        // from the version store and READPAST could not skip locked rows. Forcing
        // lock-based READ COMMITTED makes the dequeue take real locks either way.
        // INDEX + MAXDOP 1: one-row seek, never a parallel dequeue plan.
        var sql = $"""
            WITH next AS (
                SELECT TOP (1) *
                FROM Shifts WITH (
                    UPDLOCK, READPAST, ROWLOCK, READCOMMITTEDLOCK,
                    INDEX(IX_Shifts_PendingSelection))
                WHERE Status = 0
                  AND InstitutionId = @institutionId
                  AND BranchId IN ({branchPlaceholders})
                ORDER BY Priority DESC, CreatedAt ASC, Id ASC
            )
            UPDATE next
            SET Status = @inProgress, TakenBy = @userId, TakenAt = SYSUTCDATETIME()
            OUTPUT inserted.Id, inserted.Code, inserted.Priority, inserted.CreatedAt,
                   inserted.BranchId, inserted.InstitutionId
            OPTION (MAXDOP 1);
            """;

        var rows = await _db.Database
            .SqlQueryRaw<ClaimedRow>(sql, parameters.Cast<object>().ToArray())
            .ToListAsync(cancellationToken);

        var claimed = rows.FirstOrDefault();
        if (claimed is null)
        {
            _logger.LogInformation("Take-next found no pending shift for user {UserId}", _currentUser.Id);
            return ShiftResult<ClaimedShiftResponse>.Fail(ShiftOutcome.NoneAvailable);
        }

        _logger.LogInformation("User {UserId} claimed shift {ShiftId}", _currentUser.Id, claimed.Id);
        return ShiftResult<ClaimedShiftResponse>.Ok(new ClaimedShiftResponse
        {
            Id = claimed.Id,
            Code = claimed.Code,
            Priority = claimed.Priority,
            CreatedAt = claimed.CreatedAt,
            BranchId = claimed.BranchId,
            InstitutionId = claimed.InstitutionId
        });
    }

    /// <inheritdoc />
    public async Task<ShiftResult<object?>> FinishAsync(int id, CancellationToken cancellationToken)
    {
        if (_currentUser.AuthorizedBranchIds.Count == 0)
        {
            _logger.LogWarning("Finish forbidden for user {UserId}: no authorized branches", _currentUser.Id);
            return ShiftResult<object?>.Fail(ShiftOutcome.Forbidden);
        }

        var parameters = new List<SqlParameter>
        {
            IntParameter("completed", (int)ShiftStatus.Completed),
            IntParameter("inProgress", (int)ShiftStatus.InProgress),
            IntParameter("id", id),
            IntParameter("institutionId", _currentUser.InstitutionId)
        };

        var branchPlaceholders = AddBranchParameters(parameters, _currentUser.AuthorizedBranchIds);

        var sql = $"""
            UPDATE Shifts
            SET Status = @completed, CompletedAt = SYSUTCDATETIME()
            WHERE Id = @id
              AND InstitutionId = @institutionId
              AND BranchId IN ({branchPlaceholders})
              AND Status = @inProgress;
            """;

        var affected = await _db.Database.ExecuteSqlRawAsync(
            sql,
            parameters.Cast<object>(),
            cancellationToken);
        if (affected == 1)
        {
            _logger.LogInformation("User {UserId} finished shift {ShiftId}", _currentUser.Id, id);
            return ShiftResult<object?>.Ok(null);
        }

        return await MapFailedFinishAsync(id, cancellationToken);
    }

    /// <summary>
    /// Distinguishes 404 / 403 / 409 after a finish UPDATE that affected zero rows.
    /// </summary>
    /// <param name="id">Requested shift identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The mapped failure outcome.</returns>
    private async Task<ShiftResult<object?>> MapFailedFinishAsync(int id, CancellationToken cancellationToken)
    {
        var shift = await _db.Shifts
            .AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new { s.Id, s.InstitutionId, s.BranchId, s.Status })
            .FirstOrDefaultAsync(cancellationToken);

        if (shift is null)
        {
            _logger.LogInformation("Finish target {ShiftId} not found for user {UserId}", id, _currentUser.Id);
            return ShiftResult<object?>.Fail(ShiftOutcome.NotFound);
        }

        if (shift.InstitutionId != _currentUser.InstitutionId
            || !_currentUser.AuthorizedBranchIds.Contains(shift.BranchId))
        {
            _logger.LogWarning("Finish out of scope for user {UserId} on shift {ShiftId}", _currentUser.Id, id);
            return ShiftResult<object?>.Fail(ShiftOutcome.Forbidden);
        }

        _logger.LogWarning(
            "Finish conflict for user {UserId} on shift {ShiftId} with status {Status}",
            _currentUser.Id,
            id,
            (int)shift.Status);
        return ShiftResult<object?>.Fail(ShiftOutcome.Conflict);
    }

    /// <summary>
    /// Appends one SQL parameter per authorized branch and returns the
    /// <c>IN (...)</c> placeholder list. Values are never concatenated into SQL.
    /// </summary>
    /// <param name="parameters">Parameter list to extend.</param>
    /// <param name="branchIds">Authorized branch identifiers.</param>
    /// <returns>Comma-separated parameter names such as <c>@b0, @b1</c>.</returns>
    private static string AddBranchParameters(List<SqlParameter> parameters, IReadOnlyList<int> branchIds)
    {
        var names = new string[branchIds.Count];
        for (var i = 0; i < branchIds.Count; i++)
        {
            var name = $"b{i}";
            names[i] = "@" + name;
            parameters.Add(IntParameter(name, branchIds[i]));
        }

        return string.Join(", ", names);
    }

    /// <summary>
    /// Creates an <c>int</c> SQL parameter. Must set <see cref="SqlDbType"/>
    /// explicitly: <c>new SqlParameter(name, 0)</c> binds the
    /// <see cref="SqlDbType"/> overload (0 = BigInt) and supplies no value.
    /// </summary>
    /// <param name="name">Parameter name without <c>@</c>.</param>
    /// <param name="value">Integer value, including zero.</param>
    /// <returns>A parameterized integer argument.</returns>
    private static SqlParameter IntParameter(string name, int value)
    {
        return new SqlParameter(name, SqlDbType.Int) { Value = value };
    }
}
