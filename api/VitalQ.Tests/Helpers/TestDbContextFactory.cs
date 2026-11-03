using Microsoft.EntityFrameworkCore;
using VitalQ.DataAccess;
using VitalQ.Entities.Models;
using DoctorEntity = VitalQ.Entities.Models.Doctor;
using PatientEntity = VitalQ.Entities.Models.Patient;

namespace VitalQ.Tests.Helpers;

public class TestVitalQDbContext : VitalQDbContext
{
    public TestVitalQDbContext(DbContextOptions<VitalQDbContext> options) : base(options)
    {
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        PopulateRowVersions();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        PopulateRowVersions();
        return base.SaveChanges();
    }

    private void PopulateRowVersions()
    {
        foreach (var entry in ChangeTracker.Entries<QueueToken>())
        {
            if (entry.Entity.RowVersion == null || entry.Entity.RowVersion.Length == 0)
            {
                entry.Entity.RowVersion = new byte[] { 1, 0, 0, 0 };
            }
        }
    }
}

public static class TestDbContextFactory
{
    public static VitalQDbContext Create(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<VitalQDbContext>()
            .UseInMemoryDatabase(databaseName: dbName ?? Guid.NewGuid().ToString())
            .EnableSensitiveDataLogging()
            .Options;

        var context = new TestVitalQDbContext(options);
        return context;
    }

    public static QueueToken CreateToken(
        Guid deptId,
        Guid doctorId,
        Guid patientId,
        string tokenNumber = "CARD-001",
        string status = "Booked",
        int priorityScore = 100,
        int baseWeight = 100,
        DateTime? bookedAtUtc = null,
        DateTime? triagedAtUtc = null,
        DateTime? calledAtUtc = null,
        DateTime? completedAtUtc = null,
        string? notes = null)
    {
        return new QueueToken
        {
            Id = Guid.NewGuid(),
            TokenNumber = tokenNumber,
            DepartmentId = deptId,
            DoctorId = doctorId,
            PatientId = patientId,
            Status = status,
            PriorityScore = priorityScore,
            BaseWeight = baseWeight,
            BookedAtUtc = bookedAtUtc ?? DateTime.UtcNow,
            TriagedAtUtc = triagedAtUtc,
            CalledAtUtc = calledAtUtc,
            CompletedAtUtc = completedAtUtc,
            ConsultationNotes = notes,
            RowVersion = new byte[] { 1, 0, 0, 0 }
        };
    }

    public static async Task<(Department dept, DoctorEntity doctor, User doctorUser, PatientEntity patient)> SeedTestClinicAsync(VitalQDbContext context)
    {
        var dept = new Department
        {
            Id = Guid.NewGuid(),
            Code = "CARD",
            Name = "Cardiology",
            LocationFloor = "Floor 2",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var doctorUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "dr.smith",
            FullName = "Dr. John Smith",
            Role = "Doctor",
            PhoneNumber = "1112223333",
            Password = "hashed",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var doctor = new DoctorEntity
        {
            Id = Guid.NewGuid(),
            UserId = doctorUser.Id,
            DepartmentId = dept.Id,
            Specialization = "Cardiologist",
            RoomNumber = "201",
            Status = "Available",
            AvgConsultationMinutes = 10,
            CreatedAtUtc = DateTime.UtcNow
        };

        var patient = new PatientEntity
        {
            Id = Guid.NewGuid(),
            MedicalRecordNumber = "MRN-000001",
            FullName = "Jane Doe",
            PhoneNumber = "9998887777",
            Gender = "Female",
            DateOfBirth = new DateOnly(1990, 5, 15),
            CreatedAtUtc = DateTime.UtcNow
        };

        await context.Departments.AddAsync(dept);
        await context.Users.AddAsync(doctorUser);
        await context.Doctors.AddAsync(doctor);
        await context.Patients.AddAsync(patient);
        await context.SaveChangesAsync();

        return (dept, doctor, doctorUser, patient);
    }
}
