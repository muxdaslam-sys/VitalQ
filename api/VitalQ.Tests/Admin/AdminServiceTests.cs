using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using VitalQ.BusinessLogic.Services;
using VitalQ.Entities.DTOs;
using VitalQ.Entities.Models;
using VitalQ.Tests.Helpers;
using DoctorEntity = VitalQ.Entities.Models.Doctor;
using PatientEntity = VitalQ.Entities.Models.Patient;

namespace VitalQ.Tests.Admin;

public class AdminServiceTests
{
    private static IMemoryCache CreateMemoryCache()
    {
        return new MemoryCache(new MemoryCacheOptions());
    }

    [Fact]
    public async Task GetDashboardStatsAsync_ReturnsAccurateEntityCounts()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var cache = CreateMemoryCache();

        var dept = new Department
        {
            Id = Guid.NewGuid(),
            Name = "Cardiology",
            Code = "CARD",
            LocationFloor = "Floor 2",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        await context.Departments.AddAsync(dept);

        var station = new NursingStation
        {
            Id = Guid.NewGuid(),
            StationName = "Station A",
            DepartmentId = dept.Id,
            LocationFloor = "Floor 2",
            IsActive = true
        };
        await context.NursingStations.AddAsync(station);

        var adminUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "admin.user",
            Password = "password",
            FullName = "Admin User",
            PhoneNumber = "1111111111",
            Role = "Admin",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        var nurseUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "nurse.user",
            Password = "password",
            FullName = "Nurse User",
            PhoneNumber = "2222222222",
            Role = "Nurse",
            NursingStationId = station.Id,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        await context.Users.AddRangeAsync(adminUser, nurseUser);

        var doctorUser1 = new User
        {
            Id = Guid.NewGuid(),
            Username = "dr.one",
            Password = "password",
            FullName = "Dr. One",
            PhoneNumber = "3333333331",
            Role = "Doctor",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        var doctorUser2 = new User
        {
            Id = Guid.NewGuid(),
            Username = "dr.two",
            Password = "password",
            FullName = "Dr. Two",
            PhoneNumber = "3333333332",
            Role = "Doctor",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        await context.Users.AddRangeAsync(doctorUser1, doctorUser2);

        var doc1 = new DoctorEntity
        {
            Id = Guid.NewGuid(),
            UserId = doctorUser1.Id,
            DepartmentId = dept.Id,
            Specialization = "Cardiologist",
            RoomNumber = "101",
            Status = "Available",
            AvgConsultationMinutes = 15,
            CreatedAtUtc = DateTime.UtcNow
        };
        var doc2 = new DoctorEntity
        {
            Id = Guid.NewGuid(),
            UserId = doctorUser2.Id,
            DepartmentId = dept.Id,
            Specialization = "Cardiologist",
            RoomNumber = "102",
            Status = "Busy",
            AvgConsultationMinutes = 15,
            CreatedAtUtc = DateTime.UtcNow
        };
        await context.Doctors.AddRangeAsync(doc1, doc2);

        var patient = new PatientEntity
        {
            Id = Guid.NewGuid(),
            MedicalRecordNumber = "MRN-000001",
            FullName = "Patient One",
            PhoneNumber = "4444444444",
            Gender = "Male",
            DateOfBirth = new DateOnly(1990, 1, 1),
            CreatedAtUtc = DateTime.UtcNow
        };
        await context.Patients.AddAsync(patient);
        await context.SaveChangesAsync();

        var service = new AdminService(context, cache);

        // Act
        var stats = await service.GetDashboardStatsAsync();

        // Assert
        stats.Should().NotBeNull();
        stats.TotalDoctors.Should().Be(2);
        stats.AvailableDoctors.Should().Be(1);
        stats.TotalStaff.Should().Be(2);
        stats.TotalPatients.Should().Be(1);
        stats.TotalDepartments.Should().Be(1);
        stats.TotalNursingStations.Should().Be(1);
    }

    [Fact]
    public async Task CreateAdminUserAsync_ValidRequest_CreatesUserSuccessfully()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var service = new AdminService(context, CreateMemoryCache());

        var request = new CreateUserRequest
        {
            Username = "new.admin",
            Password = "AdminPassword123!",
            FullName = "New Administrator",
            PhoneNumber = "9998881111",
            Role = "Admin"
        };

        // Act
        var result = await service.CreateAdminUserAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Username.Should().Be("new.admin");
        result.Role.Should().Be("Admin");
        result.IsActive.Should().BeTrue();

        var dbUser = await context.Users.FirstOrDefaultAsync(u => u.Username == "new.admin");
        dbUser.Should().NotBeNull();
        dbUser!.FullName.Should().Be("New Administrator");
    }

    [Fact]
    public async Task CreateAdminUserAsync_DuplicateUsername_ThrowsInvalidOperationException()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var existingUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "duplicate.user",
            Password = "password",
            FullName = "Existing User",
            PhoneNumber = "9998882222",
            Role = "Admin",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        await context.Users.AddAsync(existingUser);
        await context.SaveChangesAsync();

        var service = new AdminService(context, CreateMemoryCache());
        var request = new CreateUserRequest
        {
            Username = "duplicate.user",
            Password = "password",
            FullName = "Another User",
            PhoneNumber = "9998883333",
            Role = "Admin"
        };

        // Act
        var act = async () => await service.CreateAdminUserAsync(request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already in use*");
    }

    [Fact]
    public async Task CreateAdminUserAsync_NurseWithoutStation_ThrowsArgumentException()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var service = new AdminService(context, CreateMemoryCache());
        var request = new CreateUserRequest
        {
            Username = "nurse.nostation",
            Password = "password",
            FullName = "Nurse No Station",
            PhoneNumber = "9998884444",
            Role = "Nurse",
            NursingStationId = null
        };

        // Act
        var act = async () => await service.CreateAdminUserAsync(request);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Nursing Station must be assigned*");
    }

    [Fact]
    public async Task CreateDoctorAsync_ValidRequest_CreatesUserAndDoctorRecord()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var dept = new Department
        {
            Id = Guid.NewGuid(),
            Name = "Cardiology",
            Code = "CARD",
            LocationFloor = "Floor 2",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        await context.Departments.AddAsync(dept);
        await context.SaveChangesAsync();

        var service = new AdminService(context, CreateMemoryCache());
        var request = new CreateDoctorRequest
        {
            Username = "dr.cardio",
            Password = "DoctorPassword123!",
            FullName = "Dr. Cardio",
            PhoneNumber = "9998885555",
            DepartmentId = dept.Id,
            Specialization = "Interventional Cardiology",
            RoomNumber = "205",
            AvgConsultationMinutes = 20
        };

        // Act
        var result = await service.CreateDoctorAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.DoctorName.Should().Be("Dr. Cardio");
        result.Specialization.Should().Be("Interventional Cardiology");
        result.RoomNumber.Should().Be("205");
        result.DepartmentName.Should().Be("Cardiology");
        result.Status.Should().Be("Available");

        var dbDoctor = await context.Doctors.Include(d => d.User).FirstOrDefaultAsync(d => d.Id == result.Id);
        dbDoctor.Should().NotBeNull();
        dbDoctor!.User.Username.Should().Be("dr.cardio");
    }

    [Fact]
    public async Task UpdateDoctorStatusAsync_ValidStatus_UpdatesStatusSuccessfully()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var (dept, doctor, _, _) = await TestDbContextFactory.SeedTestClinicAsync(context);
        var service = new AdminService(context, CreateMemoryCache());

        // Act
        var updated = await service.UpdateDoctorStatusAsync(doctor.Id, new UpdateDoctorStatusRequest
        {
            Status = "OnLeave"
        });

        // Assert
        updated.Status.Should().Be("OnLeave");
        var dbDoctor = await context.Doctors.FindAsync(doctor.Id);
        dbDoctor!.Status.Should().Be("OnLeave");
    }

    [Fact]
    public async Task UpdateDoctorStatusAsync_InvalidStatus_ThrowsArgumentException()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var (dept, doctor, _, _) = await TestDbContextFactory.SeedTestClinicAsync(context);
        var service = new AdminService(context, CreateMemoryCache());

        // Act
        var act = async () => await service.UpdateDoctorStatusAsync(doctor.Id, new UpdateDoctorStatusRequest
        {
            Status = "Unavailable"
        });

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Invalid doctor status*");
    }

    [Fact]
    public async Task CreateDepartmentAsync_ValidRequest_CreatesDepartmentSuccessfully()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var service = new AdminService(context, CreateMemoryCache());
        var request = new CreateDepartmentRequest
        {
            Name = "Neurology",
            Code = "NEUR",
            LocationFloor = "Floor 4"
        };

        // Act
        var result = await service.CreateDepartmentAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Code.Should().Be("NEUR");
        result.Name.Should().Be("Neurology");
        result.LocationFloor.Should().Be("Floor 4");

        var dbDept = await context.Departments.FirstOrDefaultAsync(d => d.Code == "NEUR");
        dbDept.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateDepartmentAsync_DuplicateCode_ThrowsInvalidOperationException()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var dept = new Department
        {
            Id = Guid.NewGuid(),
            Name = "Neurology",
            Code = "NEUR",
            LocationFloor = "Floor 4",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        await context.Departments.AddAsync(dept);
        await context.SaveChangesAsync();

        var service = new AdminService(context, CreateMemoryCache());
        var request = new CreateDepartmentRequest
        {
            Name = "New Neurology",
            Code = "neur", // Lowercase test
            LocationFloor = "Floor 5"
        };

        // Act
        var act = async () => await service.CreateDepartmentAsync(request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already in use*");
    }

    [Fact]
    public async Task CreateNursingStationAsync_ValidRequest_CreatesNursingStation()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var dept = new Department
        {
            Id = Guid.NewGuid(),
            Name = "Emergency",
            Code = "EMER",
            LocationFloor = "Ground Floor",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        await context.Departments.AddAsync(dept);
        await context.SaveChangesAsync();

        var service = new AdminService(context, CreateMemoryCache());
        var request = new CreateNursingStationRequest
        {
            StationName = "Station Emergency 1",
            DepartmentId = dept.Id,
            LocationFloor = "Ground Floor"
        };

        // Act
        var result = await service.CreateNursingStationAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.StationName.Should().Be("Station Emergency 1");
        result.DepartmentName.Should().Be("Emergency");

        var dbStation = await context.NursingStations.FirstOrDefaultAsync(s => s.StationName == "Station Emergency 1");
        dbStation.Should().NotBeNull();
    }
}
