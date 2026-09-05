# Shift Management API (ERP SPM)

Small ASP.NET Core Web API for taking and finishing shifts (`turnos`) in a multi-institution, multi-branch ERP. There is no frontend. A user may only see or change shifts that belong to their own institution and authorized branches. Institution and branch scope are derived server-side from `X-User-Id`; the client never supplies them.

## Routes

| Method | Route | Purpose |
| --- | --- | --- |
| `GET` | `/api/turnos/pendientes` | List pending shifts in the caller's scope |
| `POST` | `/api/turnos/tomar-siguiente` | Claim the next pending shift |
| `POST` | `/api/turnos/{id}/finalizar` | Finish an in-progress shift |

Write operations accept **no request body**. The only client inputs are `X-User-Id` and, for finish, the route `id`. Accepting a body with institution or branch fields would violate the rule that scope is never trusted from the client.

Seed users for local calls:

| `X-User-Id` | Name | Scope |
| --- | --- | --- |
| `1` | Ana Torres | Institution 1, branch 1 |
| `2` | Beto Ramirez | Institution 1, branches 1 and 2 |
| `3` | Carla Nunez | Institution 2, branch 3 |

## Run locally

1. Clone the repository.
2. Start SQL Server 2022. Compose reads `MSSQL_SA_PASSWORD` from the root `.env`:

```bash
docker compose up -d
```

Wait until the engine accepts connections (first start can take about 20–30 seconds).

3. Run the API (Development applies migrations and seed on startup):

```bash
dotnet run --project src/Shifts.Api
```

Swagger is at `/swagger` in Development.

Explicit migrate alternative (after `dotnet tool restore`):

```bash
dotnet tool restore
dotnet ef database update --project src/Shifts.Api --startup-project src/Shifts.Api
```

If you previously started the compose stack with an older password, recreate the volume so SQL Server picks up the current `.env` value:

```bash
docker compose down -v
docker compose up -d
```

## `.env` (reviewer exception)

`MSSQL_SA_PASSWORD` is committed in the root `.env` so a reviewer can clone and run the stack. It is a **disposable local credential**, not a production secret. Do not treat it as one.

The API loads that file with DotNetEnv using an **explicit path**: it walks up from the application base directory until it finds the `.sln` or `.git` folder, then loads that `.env`. It does not call `Env.Load()` with no arguments, so `dotnet run --project src/Shifts.Api` works from the repo root or from `src/Shifts.Api`.

The connection string name is `ShiftsDb`. Host, database, and user stay in configuration; the password is applied from `MSSQL_SA_PASSWORD` only. There is no second password.

## Concurrency strategy

Taking the next shift is a work-queue claim, not an optimistic `rowversion` update.

One parameterized statement selects the next pending row with `UPDLOCK, READPAST, ROWLOCK, READCOMMITTEDLOCK` and updates it in the same statement (`OUTPUT` returns the claimed row). `UPDLOCK` locks the chosen row for update. `READPAST` makes concurrent callers **skip** already-locked rows and claim the next available one, so two callers get different shifts. A single statement is atomic; no extra `BEGIN/COMMIT` is required.

Three details make that dequeue correct under EF Core's SQL Server defaults:

- `Status = 0` is a **literal** (`ShiftStatus.Pending`), not `@pending`. A parameter cannot match the filtered index `IX_Shifts_PendingSelection` (`[Status] = 0`). Without that seek the plan is scan + sort, `UPDLOCK` covers every pending row, and concurrent `READPAST` callers see an empty queue (HTTP 404) while other pending shifts still exist.
- `READCOMMITTEDLOCK` forces lock-based `READ COMMITTED`. EF enables `READ_COMMITTED_SNAPSHOT`; version-store reads plus `TOP (1)` + `READPAST` can also report a false empty queue.
- `INDEX(IX_Shifts_PendingSelection)` and `OPTION (MAXDOP 1)` keep the plan a single-row seek, never a parallel dequeue.

Optimistic `rowversion` was rejected because every worker targets the same "next" row, which turns into a retry storm on a hot row.

Take-next uses `Database.SqlQueryRaw<ClaimedRow>` so the `OUTPUT` row is kept. Finish uses `ExecuteSqlRaw` because only the affected-row count is needed. Those two APIs are not unified: `ExecuteSqlRaw` would drop the claimed row.

## Selection index

`IX_Shifts_PendingSelection` is a filtered index on `(InstitutionId, BranchId, Priority DESC, CreatedAt ASC, Id ASC)` with `[Status] = 0` (Pending). It matches the authorization filter and the deterministic sort, and it only covers the hot pending subset, so take-next can seek instead of scan.

## Tests

```bash
docker compose up -d
dotnet test tests/Shifts.Tests
```

Tests use `WebApplicationFactory` against a dedicated catalog `ShiftsDb_Tests` on the same SQL Server. They do not use the EF in-memory provider (it does not honor `UPDLOCK` / `READPAST`). Classes share an xUnit collection and the test project disables parallelization so they cannot drop the same database at the same time.

## Technical decisions to defend

- Server-side scope from `X-User-Id` + `UserBranch`; never from the client body or query.
- `SqlQueryRaw<ClaimedRow>` for take-next vs `ExecuteSqlRaw` for finish.
- No write-body DTOs (rule 2).
- Pessimistic `UPDLOCK, READPAST` claim instead of `rowversion`.
- Filtered pending-selection index.
- `ICurrentUser` is registered as a factory over the same scoped `CurrentUser` instance. Middleware resolves `CurrentUser` from `RequestServices` and writes it; it does not constructor-inject the scoped user.

## AI use

Cursor was used as an assistant. Architecture, concurrency, security, and persistence decisions are owned and can be defended.

## What I would improve with more time

- Replace the simulated header with real authentication while keeping server-side scope derivation.
- Use a table-valued parameter for large authorized-branch lists instead of one SQL parameter per id.
- Add an audit trail of claims and finishes without changing the three required endpoints.
- A dedicated design-time `DbContext` factory so `dotnet ef` never touches a live database.
