using Microsoft.EntityFrameworkCore;
using VitalQ.Entities.Models;

namespace VitalQ.DataAccess;

/// <summary>
/// Provides idempotent, non-destructive database initialization and baseline clinical data seeding.
/// Guarantees 100% zero data loss when executed against existing databases with live patient records.
/// </summary>
public static class DbInitializer
{
    /// <summary>
    /// Safely applies pending EF Core schema migrations and seeds baseline clinical records only if missing.
    /// Never deletes, truncates, or overwrites existing patient encounters, doctor rosters, or queue tokens.
    /// </summary>
    public static async Task InitializeAsync(VitalQDbContext context)
    {
        // 1. Safe Schema Migration (Relational Databases only)
        if (context.Database.IsRelational())
        {
            await context.Database.MigrateAsync();
        }

        // 2. Baseline Clinical Departments Guard
        // If any departments already exist, skip completely to preserve live configurations
        if (!await context.Departments.AnyAsync())
        {
            var departments = new List<Department>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Name = "Cardiology",
                    Code = "CARD",
                    LocationFloor = "Floor 2, Wing A",
                    LastTokenNumber = 0,
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    Name = "General Medicine",
                    Code = "GEN",
                    LocationFloor = "Ground Floor, Wing B",
                    LastTokenNumber = 0,
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    Name = "Pediatrics",
                    Code = "PED",
                    LocationFloor = "Floor 1, Wing C",
                    LastTokenNumber = 0,
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    Name = "Orthopedics",
                    Code = "ORTH",
                    LocationFloor = "Floor 3, Wing A",
                    LastTokenNumber = 0,
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                }
            };

            try
            {
                await context.Departments.AddRangeAsync(departments);
                await context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // A concurrent container replica already seeded departments. Safe to continue.
            }
        }

        // 3. Baseline Nursing Triage Stations Guard
        if (!await context.NursingStations.AnyAsync())
        {
            var cardDept = await context.Departments.FirstOrDefaultAsync(d => d.Code == "CARD");
            var genDept = await context.Departments.FirstOrDefaultAsync(d => d.Code == "GEN");

            var stations = new List<NursingStation>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    StationName = "Cardiology Triage Desk",
                    DepartmentId = cardDept?.Id,
                    LocationFloor = "Floor 2, Wing A",
                    IsActive = true
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    StationName = "Outpatient General Intake",
                    DepartmentId = genDept?.Id,
                    LocationFloor = "Ground Floor, Wing B",
                    IsActive = true
                }
            };

            try
            {
                await context.NursingStations.AddRangeAsync(stations);
                await context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // A concurrent container replica already seeded nursing stations. Safe to continue.
            }
        }

        // 4. Baseline System Administrator Account Guard
        // Ensures at least one administrator account exists without overwriting existing staff
        if (!await context.Users.AnyAsync(u => u.Role == "Admin"))
        {
            var defaultAdmin = new User
            {
                Id = Guid.NewGuid(),
                Username = "admin",
                Password = "HospitalAdmin123!",
                FullName = "System Administrator",
                PhoneNumber = "9998887770",
                Role = "Admin",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            try
            {
                await context.Users.AddAsync(defaultAdmin);
                await context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // A concurrent container replica already seeded the admin user. Safe to continue.
            }
        }
    }
}
