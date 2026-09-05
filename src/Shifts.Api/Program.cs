using Microsoft.EntityFrameworkCore;
using Shifts.Api.Data;
using Shifts.Api.Errors;
using Shifts.Api.Features.Shifts;
using Shifts.Api.Infrastructure;
using Shifts.Api.Security;

RepoEnvLoader.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var shiftsConnectionString = builder.Configuration.GetConnectionString("ShiftsDb")
    ?? throw new InvalidOperationException("Connection string 'ShiftsDb' is not configured.");
shiftsConnectionString = RepoEnvLoader.ApplyPassword(shiftsConnectionString);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        shiftsConnectionString,
        // Retry transient failures so the API tolerates SQL Server still
        // warming up right after `docker compose up -d`.
        sql => sql.EnableRetryOnFailure()));

builder.Services.AddScoped<CurrentUser>();
builder.Services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<CurrentUser>());
builder.Services.AddScoped<IShiftService, ShiftService>();

var app = builder.Build();

app.UseExceptionHandler();

// In Development, apply migrations (and seed) on startup so the reviewer's
// whole flow is: `docker compose up -d`  ->  `dotnet run`.
if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.Migrate();
    }

    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseMiddleware<CurrentUserMiddleware>();
app.MapControllers();

app.Run();

// Exposed so the integration test project can spin up the API in-memory
// via WebApplicationFactory<Program> (needed for the concurrency test).
public partial class Program { }
