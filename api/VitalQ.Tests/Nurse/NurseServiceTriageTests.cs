using VitalQ.BusinessLogic.Services;
using VitalQ.Entities.DTOs;
using VitalQ.Entities.Models;
using VitalQ.Tests.Helpers;

namespace VitalQ.Tests.Nurse;

public class NurseServiceTriageTests
{
    [Fact]
    public async Task RecordVitalsAndTriageAsync_SevereHypoxia_AssignsRedAndBaseWeight1000()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var (dept, doctor, doctorUser, patient) = await TestDbContextFactory.SeedTestClinicAsync(context);

        var nurseUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "nurse.test",
            FullName = "Nurse Sarah",
            Role = "Nurse",
            PhoneNumber = "5551112222",
            Password = "hash",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        await context.Users.AddAsync(nurseUser);

        var token = new QueueToken
        {
            RowVersion = new byte[] { 1, 0, 0, 0 },
            Id = Guid.NewGuid(),
            TokenNumber = "CARD-001",
            PatientId = patient.Id,
            DepartmentId = dept.Id,
            DoctorId = doctor.Id,
            Status = "Booked",
            BookedAtUtc = DateTime.UtcNow
        };
        await context.QueueTokens.AddAsync(token);
        await context.SaveChangesAsync();

        var service = new NurseService(context);
        var request = new TriageRequest
        {
            SpO2 = 88, // Severe hypoxia (< 90%) -> Red!
            SystolicBp = 120,
            DiastolicBp = 80,
            HeartRate = 80,
            Temperature = 37.0m,
            PainScale = 3,
            IsManualOverride = false
        };

        // Act
        var result = await service.RecordVitalsAndTriageAsync(token.Id, nurseUser.Id, request);

        // Assert
        result.Status.Should().Be("Waiting");
        result.TriageLevel.Should().Be("Red");
        result.BaseWeight.Should().Be(1000);
        result.PriorityScore.Should().Be(1000);
        result.TriagedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task RecordVitalsAndTriageAsync_ModerateHypoxia_AssignsYellowAndBaseWeight500()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var (dept, doctor, doctorUser, patient) = await TestDbContextFactory.SeedTestClinicAsync(context);

        var nurseUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "nurse.test",
            FullName = "Nurse Sarah",
            Role = "Nurse",
            PhoneNumber = "5551112222",
            Password = "hash",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        await context.Users.AddAsync(nurseUser);

        var token = new QueueToken
        {
            RowVersion = new byte[] { 1, 0, 0, 0 },
            Id = Guid.NewGuid(),
            TokenNumber = "CARD-001",
            PatientId = patient.Id,
            DepartmentId = dept.Id,
            DoctorId = doctor.Id,
            Status = "Booked",
            BookedAtUtc = DateTime.UtcNow
        };
        await context.QueueTokens.AddAsync(token);
        await context.SaveChangesAsync();

        var service = new NurseService(context);
        var request = new TriageRequest
        {
            SpO2 = 92, // Borderline (90 - 94%) -> Yellow!
            SystolicBp = 125,
            DiastolicBp = 82,
            HeartRate = 85,
            Temperature = 37.2m,
            PainScale = 4,
            IsManualOverride = false
        };

        // Act
        var result = await service.RecordVitalsAndTriageAsync(token.Id, nurseUser.Id, request);

        // Assert
        result.TriageLevel.Should().Be("Yellow");
        result.BaseWeight.Should().Be(500);
        result.PriorityScore.Should().Be(500);
    }

    [Fact]
    public async Task RecordVitalsAndTriageAsync_StableVitals_AssignsGreenAndBaseWeight100()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var (dept, doctor, doctorUser, patient) = await TestDbContextFactory.SeedTestClinicAsync(context);

        var nurseUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "nurse.test",
            FullName = "Nurse Sarah",
            Role = "Nurse",
            PhoneNumber = "5551112222",
            Password = "hash",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        await context.Users.AddAsync(nurseUser);

        var token = new QueueToken
        {
            RowVersion = new byte[] { 1, 0, 0, 0 },
            Id = Guid.NewGuid(),
            TokenNumber = "CARD-001",
            PatientId = patient.Id,
            DepartmentId = dept.Id,
            DoctorId = doctor.Id,
            Status = "Booked",
            BookedAtUtc = DateTime.UtcNow
        };
        await context.QueueTokens.AddAsync(token);
        await context.SaveChangesAsync();

        var service = new NurseService(context);
        var request = new TriageRequest
        {
            SpO2 = 99,
            SystolicBp = 118,
            DiastolicBp = 76,
            HeartRate = 72,
            Temperature = 36.8m,
            PainScale = 2,
            IsManualOverride = false
        };

        // Act
        var result = await service.RecordVitalsAndTriageAsync(token.Id, nurseUser.Id, request);

        // Assert
        result.TriageLevel.Should().Be("Green");
        result.BaseWeight.Should().Be(100);
        result.PriorityScore.Should().Be(100);
    }

    [Fact]
    public async Task RecordVitalsAndTriageAsync_ManualOverrideWithoutReason_ThrowsArgumentException()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var (dept, doctor, doctorUser, patient) = await TestDbContextFactory.SeedTestClinicAsync(context);

        var nurseUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "nurse.test",
            FullName = "Nurse Sarah",
            Role = "Nurse",
            PhoneNumber = "5551112222",
            Password = "hash",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        await context.Users.AddAsync(nurseUser);

        var token = new QueueToken
        {
            RowVersion = new byte[] { 1, 0, 0, 0 },
            Id = Guid.NewGuid(),
            TokenNumber = "CARD-001",
            PatientId = patient.Id,
            DepartmentId = dept.Id,
            DoctorId = doctor.Id,
            Status = "Booked",
            BookedAtUtc = DateTime.UtcNow
        };
        await context.QueueTokens.AddAsync(token);
        await context.SaveChangesAsync();

        var service = new NurseService(context);
        var request = new TriageRequest
        {
            SpO2 = 98,
            SystolicBp = 120,
            DiastolicBp = 80,
            HeartRate = 75,
            Temperature = 37.0m,
            PainScale = 1,
            IsManualOverride = true,
            OverrideTriageLevel = "Red",
            OverrideReason = "" // Missing required reason!
        };

        // Act
        var act = async () => await service.RecordVitalsAndTriageAsync(token.Id, nurseUser.Id, request);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*override reason is mandatory*");
    }

    [Fact]
    public async Task RecordVitalsAndTriageAsync_ValidManualOverride_AppliesOverrideAcuity()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var (dept, doctor, doctorUser, patient) = await TestDbContextFactory.SeedTestClinicAsync(context);

        var nurseUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "nurse.test",
            FullName = "Nurse Sarah",
            Role = "Nurse",
            PhoneNumber = "5551112222",
            Password = "hash",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        await context.Users.AddAsync(nurseUser);

        var token = new QueueToken
        {
            RowVersion = new byte[] { 1, 0, 0, 0 },
            Id = Guid.NewGuid(),
            TokenNumber = "CARD-001",
            PatientId = patient.Id,
            DepartmentId = dept.Id,
            DoctorId = doctor.Id,
            Status = "Booked",
            BookedAtUtc = DateTime.UtcNow
        };
        await context.QueueTokens.AddAsync(token);
        await context.SaveChangesAsync();

        var service = new NurseService(context);
        var request = new TriageRequest
        {
            SpO2 = 98,
            SystolicBp = 120,
            DiastolicBp = 80,
            HeartRate = 75,
            Temperature = 37.0m,
            PainScale = 2,
            IsManualOverride = true,
            OverrideTriageLevel = "Red",
            OverrideReason = "Patient presenting with acute diaphoresis and radiating retrosternal chest pain."
        };

        // Act
        var result = await service.RecordVitalsAndTriageAsync(token.Id, nurseUser.Id, request);

        // Assert
        result.TriageLevel.Should().Be("Red");
        result.BaseWeight.Should().Be(1000);
        result.PriorityScore.Should().Be(1000);
    }

    [Fact]
    public async Task CancelTokenAsync_BookedStatus_CancelsToken()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var (dept, doctor, doctorUser, patient) = await TestDbContextFactory.SeedTestClinicAsync(context);

        var nurseUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "nurse.test",
            FullName = "Nurse Sarah",
            Role = "Nurse",
            PhoneNumber = "5551112222",
            Password = "hash",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        await context.Users.AddAsync(nurseUser);

        var token = new QueueToken
        {
            RowVersion = new byte[] { 1, 0, 0, 0 },
            Id = Guid.NewGuid(),
            TokenNumber = "CARD-001",
            PatientId = patient.Id,
            DepartmentId = dept.Id,
            DoctorId = doctor.Id,
            Status = "Booked",
            BookedAtUtc = DateTime.UtcNow
        };
        await context.QueueTokens.AddAsync(token);
        await context.SaveChangesAsync();

        var service = new NurseService(context);

        // Act
        var cancelled = await service.CancelTokenAsync(token.Id, nurseUser.Id);

        // Assert
        cancelled.Status.Should().Be("Cancelled");
        cancelled.ConsultationNotes.Should().Be("Cancelled at Nursing Desk");
    }

    [Fact]
    public async Task CancelTokenAsync_WaitingStatus_TransfersToCasualtyWithCustomReason()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var (dept, doctor, doctorUser, patient) = await TestDbContextFactory.SeedTestClinicAsync(context);

        var nurseUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "nurse.test",
            FullName = "Nurse Sarah",
            Role = "Nurse",
            PhoneNumber = "5551112222",
            Password = "hash",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        await context.Users.AddAsync(nurseUser);

        var token = new QueueToken
        {
            RowVersion = new byte[] { 1, 0, 0, 0 },
            Id = Guid.NewGuid(),
            TokenNumber = "CARD-002",
            PatientId = patient.Id,
            DepartmentId = dept.Id,
            DoctorId = doctor.Id,
            Status = "Waiting",
            BaseWeight = 500,
            PriorityScore = 510,
            BookedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            TriagedAtUtc = DateTime.UtcNow
        };
        await context.QueueTokens.AddAsync(token);
        await context.SaveChangesAsync();

        var service = new NurseService(context);

        // Act
        var cancelled = await service.CancelTokenAsync(token.Id, nurseUser.Id, "Transferred to Casualty / Emergency Ward");

        // Assert
        cancelled.Status.Should().Be("Cancelled");
        cancelled.ConsultationNotes.Should().Be("Cancelled at Nursing Desk: Transferred to Casualty / Emergency Ward");
    }

    [Fact]
    public async Task CancelTokenAsync_CompletedToken_ThrowsInvalidOperationException()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var (dept, doctor, doctorUser, patient) = await TestDbContextFactory.SeedTestClinicAsync(context);

        var nurseUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "nurse.test",
            FullName = "Nurse Sarah",
            Role = "Nurse",
            PhoneNumber = "5551112222",
            Password = "hash",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        await context.Users.AddAsync(nurseUser);

        var token = new QueueToken
        {
            RowVersion = new byte[] { 1, 0, 0, 0 },
            Id = Guid.NewGuid(),
            TokenNumber = "CARD-003",
            PatientId = patient.Id,
            DepartmentId = dept.Id,
            DoctorId = doctor.Id,
            Status = "Completed",
            BookedAtUtc = DateTime.UtcNow.AddHours(-1),
            CompletedAtUtc = DateTime.UtcNow
        };
        await context.QueueTokens.AddAsync(token);
        await context.SaveChangesAsync();

        var service = new NurseService(context);

        // Act
        var act = async () => await service.CancelTokenAsync(token.Id, nurseUser.Id);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot cancel a consultation that has already been completed*");
    }
}
