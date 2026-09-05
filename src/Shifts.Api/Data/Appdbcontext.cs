using Microsoft.EntityFrameworkCore;
using Shifts.Api.Domain.Entities;
using Shifts.Api.Domain.Enums;

namespace Shifts.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Institution> Institutions => Set<Institution>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserBranch> UserBranches => Set<UserBranch>();
    public DbSet<Shift> Shifts => Set<Shift>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureInstitution(modelBuilder);
        ConfigureBranch(modelBuilder);
        ConfigureUser(modelBuilder);
        ConfigureUserBranch(modelBuilder);
        ConfigureShift(modelBuilder);
        Seed(modelBuilder);
    }

    private static void ConfigureInstitution(ModelBuilder b) =>
        b.Entity<Institution>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
        });

    private static void ConfigureBranch(ModelBuilder b) =>
        b.Entity<Branch>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);

            e.HasOne(x => x.Institution)
             .WithMany(i => i.Branches)
             .HasForeignKey(x => x.InstitutionId)
             .OnDelete(DeleteBehavior.Restrict);
        });

    private static void ConfigureUser(ModelBuilder b) =>
        b.Entity<User>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.Role).IsRequired().HasMaxLength(100);

            e.HasOne(x => x.Institution)
             .WithMany(i => i.Users)
             .HasForeignKey(x => x.InstitutionId)
             .OnDelete(DeleteBehavior.Restrict);
        });

    private static void ConfigureUserBranch(ModelBuilder b) =>
        b.Entity<UserBranch>(e =>
        {
            // Composite primary key: a user is linked to a branch at most once.
            e.HasKey(x => new { x.UserId, x.BranchId });

            e.HasOne(x => x.User)
             .WithMany(u => u.AuthorizedBranches)
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Branch)
             .WithMany(br => br.AuthorizedUsers)
             .HasForeignKey(x => x.BranchId)
             .OnDelete(DeleteBehavior.Restrict);
        });

    private static void ConfigureShift(ModelBuilder b) =>
        b.Entity<Shift>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).IsRequired().HasMaxLength(50);
            e.Property(x => x.Status).HasConversion<int>();

            // All shift relationships use Restrict: shifts are business records
            // in an ERP and must never be silently cascade-deleted. It also keeps
            // SQL Server free of multiple-cascade-path errors (Institution is
            // reachable both directly and via Branch).
            e.HasOne(x => x.Institution)
             .WithMany()
             .HasForeignKey(x => x.InstitutionId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Branch)
             .WithMany()
             .HasForeignKey(x => x.BranchId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.TakenByUser)
             .WithMany()
             .HasForeignKey(x => x.TakenBy)
             .OnDelete(DeleteBehavior.Restrict);

            // Filtered index for the "take next pending shift" hot path.
            // Only Pending rows are indexed (a small, always-hot subset), with the
            // key ordered to match the deterministic selection exactly:
            //   Priority DESC, CreatedAt ASC, Id ASC
            // scoped by InstitutionId + BranchId (the authorization filter).
            // This turns "next pending shift" into an index seek instead of a scan.
            e.HasIndex(x => new { x.InstitutionId, x.BranchId, x.Priority, x.CreatedAt, x.Id })
             .HasDatabaseName("IX_Shifts_PendingSelection")
             .HasFilter("[Status] = 0") // 0 = ShiftStatus.Pending
             .IsDescending(false, false, true, false, false);
        });

    private static void Seed(ModelBuilder b)
    {
        // Deterministic seed: 2 institutions, 4 branches, 3 users with different
        // scopes, and pending shifts crafted so the ordering rule is testable.
        // Fixed timestamps (never DateTime.Now) keep migrations reproducible.
        var createdBase = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);

        b.Entity<Institution>().HasData(
            new Institution { Id = 1, Name = "Institucion Norte" },
            new Institution { Id = 2, Name = "Institucion Sur" }
        );

        b.Entity<Branch>().HasData(
            new Branch { Id = 1, InstitutionId = 1, Name = "Sucursal Central" },
            new Branch { Id = 2, InstitutionId = 1, Name = "Sucursal Aeropuerto" },
            new Branch { Id = 3, InstitutionId = 2, Name = "Sucursal Principal" },
            new Branch { Id = 4, InstitutionId = 2, Name = "Sucursal Playa" }
        );

        b.Entity<User>().HasData(
            // Institution 1 - single-branch operator (scope: Central only).
            new User { Id = 1, InstitutionId = 1, Name = "Ana Torres", Role = "Operator" },
            // Institution 1 - multi-branch supervisor (scope: Central + Aeropuerto).
            new User { Id = 2, InstitutionId = 1, Name = "Beto Ramirez", Role = "Supervisor" },
            // Institution 2 - operator (used to prove cross-institution isolation).
            new User { Id = 3, InstitutionId = 2, Name = "Carla Nunez", Role = "Operator" }
        );

        b.Entity<UserBranch>().HasData(
            new UserBranch { UserId = 1, BranchId = 1 }, // Ana  -> Central
            new UserBranch { UserId = 2, BranchId = 1 }, // Beto -> Central
            new UserBranch { UserId = 2, BranchId = 2 }, // Beto -> Aeropuerto
            new UserBranch { UserId = 3, BranchId = 3 }  // Carla -> Principal
        );

        b.Entity<Shift>().HasData(
            // Inst 1 / Branch 1 (Central): the ordering test lives here.
            // Expected claim order: Id 3 (prio 5, earliest) -> Id 2 (prio 5, later) -> Id 1 (prio 1).
            new Shift { Id = 1, InstitutionId = 1, BranchId = 1, Code = "A-001", Priority = 1, CreatedAt = createdBase,                 Status = ShiftStatus.Pending },
            new Shift { Id = 2, InstitutionId = 1, BranchId = 1, Code = "A-002", Priority = 5, CreatedAt = createdBase.AddMinutes(5), Status = ShiftStatus.Pending },
            new Shift { Id = 3, InstitutionId = 1, BranchId = 1, Code = "A-003", Priority = 5, CreatedAt = createdBase.AddMinutes(2), Status = ShiftStatus.Pending },
            // Inst 1 / Branch 2 (Aeropuerto): only Beto can reach it.
            new Shift { Id = 4, InstitutionId = 1, BranchId = 2, Code = "B-001", Priority = 3, CreatedAt = createdBase,                 Status = ShiftStatus.Pending },
            // Inst 2 / Branch 3 (Principal): only Carla -> proves isolation.
            new Shift { Id = 5, InstitutionId = 2, BranchId = 3, Code = "C-001", Priority = 9, CreatedAt = createdBase,                 Status = ShiftStatus.Pending }
        );
    }
}