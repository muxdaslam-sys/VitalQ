using Microsoft.EntityFrameworkCore;
using VitalQ.DataAccess;
using VitalQ.Entities.Models;
using VitalQ.Tests.Helpers;
using PatientEntity = VitalQ.Entities.Models.Patient;

namespace VitalQ.Tests.DataAccess;

public class DbInitializerTests
{
    [Fact]
    public async Task InitializeAsync_FreshEmptyDatabase_SeedsBaselineClinicalData()
    {
        // Arrange: fresh, empty database
        using var context = TestDbContextFactory.Create();

        // Act
        await DbInitializer.InitializeAsync(context);

        // Assert
        var departments = await context.Departments.ToListAsync();
        departments.Should().HaveCount(4);
        departments.Select(d => d.Code).Should().Contain(new[] { "CARD", "GEN", "PED", "ORTH" });

        var stations = await context.NursingStations.ToListAsync();
        stations.Should().HaveCount(2);

        var admin = await context.Users.FirstOrDefaultAsync(u => u.Role == "Admin");
        admin.Should().NotBeNull();
        admin!.Username.Should().Be("admin");
    }

    [Fact]
    public async Task InitializeAsync_ExistingDatabaseWithData_DoesNotOverwriteOrDuplicateData()
    {
        // Arrange: database with existing clinical records
        using var context = TestDbContextFactory.Create();

        var existingDept = new Department
        {
            Id = Guid.NewGuid(),
            Name = "Specialty Cardiac Center",
            Code = "CARD",
            LocationFloor = "Floor 5",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        await context.Departments.AddAsync(existingDept);

        var existingAdmin = new User
        {
            Id = Guid.NewGuid(),
            Username = "hospital.director",
            Password = "CustomDirectorPassword!",
            FullName = "Dr. Medical Director",
            PhoneNumber = "8887776660",
            Role = "Admin",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        await context.Users.AddAsync(existingAdmin);

        var existingPatient = new PatientEntity
        {
            Id = Guid.NewGuid(),
            MedicalRecordNumber = "MRN-000001",
            FullName = "Existing Chronic Patient",
            PhoneNumber = "8887776661",
            Gender = "Female",
            DateOfBirth = new DateOnly(1975, 4, 12),
            CreatedAtUtc = DateTime.UtcNow
        };
        await context.Patients.AddAsync(existingPatient);

        await context.SaveChangesAsync();

        // Act: Run initialization against existing database
        await DbInitializer.InitializeAsync(context);

        // Assert: Zero data loss, zero duplicate overwrites
        var departments = await context.Departments.ToListAsync();
        departments.Should().HaveCount(1, "existing departments must be preserved and not duplicated");
        departments[0].Name.Should().Be("Specialty Cardiac Center");

        var admins = await context.Users.Where(u => u.Role == "Admin").ToListAsync();
        admins.Should().HaveCount(1, "existing administrative users must not be overwritten");
        admins[0].Username.Should().Be("hospital.director");
        admins[0].Password.Should().Be("CustomDirectorPassword!");

        var patients = await context.Patients.ToListAsync();
        patients.Should().HaveCount(1, "patient history must remain intact");
        patients[0].MedicalRecordNumber.Should().Be("MRN-000001");
    }

    [Fact]
    public async Task InitializeAsync_MultipleSequentialExecutions_IsStrictlyIdempotent()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();

        // Act: Execute 3 times consecutively
        await DbInitializer.InitializeAsync(context);
        await DbInitializer.InitializeAsync(context);
        await DbInitializer.InitializeAsync(context);

        // Assert: Results are identical, zero duplication errors
        var departments = await context.Departments.ToListAsync();
        departments.Should().HaveCount(4);

        var stations = await context.NursingStations.ToListAsync();
        stations.Should().HaveCount(2);

        var admins = await context.Users.Where(u => u.Role == "Admin").ToListAsync();
        admins.Should().HaveCount(1);
    }
}
