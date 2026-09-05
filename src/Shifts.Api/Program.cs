using Microsoft.EntityFrameworkCore;
using Shifts.Api.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("ShiftsDb"),
        // Retry transient failures so the API tolerates SQL Server still
        // warming up right after `docker compose up -d`.
        sql => sql.EnableRetryOnFailure()));

var app = builder.Build();

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
app.MapControllers();

app.Run();

// Exposed so the integration test project can spin up the API in-memory
// via WebApplicationFactory<Program> (needed for the concurrency test).
public partial class Program { }
