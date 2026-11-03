using Microsoft.EntityFrameworkCore;
using VitalQ.BusinessLogic.Services;
using VitalQ.Entities.DTOs;
using VitalQ.Entities.Models;
using VitalQ.Tests.Helpers;
using DoctorEntity = VitalQ.Entities.Models.Doctor;

namespace VitalQ.Tests.Doctor;

public class DoctorServiceSafetyTests
{
    [Fact]
    public async Task CompleteConsultationAsync_NotesUnder5Characters_ThrowsArgumentException()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var (dept, doctor, doctorUser, patient) = await TestDbContextFactory.SeedTestClinicAsync(context);

        var token = new QueueToken
        {
            RowVersion = new byte[] { 1, 0, 0, 0 },
            Id = Guid.NewGuid(),
            TokenNumber = "CARD-001",
            PatientId = patient.Id,
            DepartmentId = dept.Id,
            DoctorId = doctor.Id,
            Status = "Called",
            PriorityScore = 100,
            BaseWeight = 100,
            BookedAtUtc = DateTime.UtcNow,
            CalledAtUtc = DateTime.UtcNow
        };
        await context.QueueTokens.AddAsync(token);
        await context.SaveChangesAsync();

        var service = new DoctorService(context);
        var request = new CompleteConsultationRequest { ConsultationNotes = "Ok" }; // Only 2 chars

        // Act
        var act = async () => await service.CompleteConsultationAsync(token.Id, doctorUser.Id, request);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*minimum 5 characters*");
    }

    [Fact]
    public async Task CompleteConsultationAsync_DifferentDoctorCaller_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var (dept, doctor, doctorUser, patient) = await TestDbContextFactory.SeedTestClinicAsync(context);

        // Another doctor
        var otherUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "dr.other",
            FullName = "Dr. Other",
            Role = "Doctor",
            PhoneNumber = "4445556666",
            Password = "hash",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        var otherDoctor = new VitalQ.Entities.Models.Doctor
        {
            Id = Guid.NewGuid(),
            UserId = otherUser.Id,
            DepartmentId = dept.Id,
            Specialization = "Cardiologist",
            RoomNumber = "202",
            Status = "Available",
            CreatedAtUtc = DateTime.UtcNow
        };
        await context.Users.AddAsync(otherUser);
        await context.Doctors.AddAsync(otherDoctor);

        var token = new QueueToken
        {
            RowVersion = new byte[] { 1, 0, 0, 0 },
            Id = Guid.NewGuid(),
            TokenNumber = "CARD-001",
            PatientId = patient.Id,
            DepartmentId = dept.Id,
            DoctorId = doctor.Id, // Assigned to Doctor Smith, NOT Other
            Status = "Called",
            PriorityScore = 100,
            BaseWeight = 100,
            BookedAtUtc = DateTime.UtcNow
        };
        await context.QueueTokens.AddAsync(token);
        await context.SaveChangesAsync();

        var service = new DoctorService(context);
        var request = new CompleteConsultationRequest { ConsultationNotes = "Patient diagnosed with mild tachycardia. Prescribed rest." };

        // Act: Other doctor tries to complete Smith's token
        var act = async () => await service.CompleteConsultationAsync(token.Id, otherUser.Id, request);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*only authorized to conclude consultations for your own assigned patients*");
    }

    [Fact]
    public async Task CompleteConsultationAsync_ValidNotesAndAuthorized_CompletesToken()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var (dept, doctor, doctorUser, patient) = await TestDbContextFactory.SeedTestClinicAsync(context);

        var token = new QueueToken
        {
            RowVersion = new byte[] { 1, 0, 0, 0 },
            Id = Guid.NewGuid(),
            TokenNumber = "CARD-001",
            PatientId = patient.Id,
            DepartmentId = dept.Id,
            DoctorId = doctor.Id,
            Status = "Called",
            PriorityScore = 100,
            BaseWeight = 100,
            BookedAtUtc = DateTime.UtcNow,
            CalledAtUtc = DateTime.UtcNow
        };
        await context.QueueTokens.AddAsync(token);
        await context.SaveChangesAsync();

        var service = new DoctorService(context);
        var request = new CompleteConsultationRequest { ConsultationNotes = "Patient recovered well. Follow up in 2 weeks." };

        // Act
        var result = await service.CompleteConsultationAsync(token.Id, doctorUser.Id, request);

        // Assert
        result.Status.Should().Be("Completed");
        result.ConsultationNotes.Should().Be("Patient recovered well. Follow up in 2 weeks.");
        result.CompletedAtUtc.Should().NotBeNull();

        var updated = await context.QueueTokens.FindAsync(token.Id);
        updated!.Status.Should().Be("Completed");
        updated.CompletedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task CallNextPatientAsync_DoctorHasActivePatientInRoomWithoutNotes_ThrowsInvalidOperationException()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var (dept, doctor, doctorUser, patient) = await TestDbContextFactory.SeedTestClinicAsync(context);

        // Patient 1 already Called and inside room
        var activeToken = new QueueToken
        {
            RowVersion = new byte[] { 1, 0, 0, 0 },
            Id = Guid.NewGuid(),
            TokenNumber = "CARD-001",
            PatientId = patient.Id,
            DepartmentId = dept.Id,
            DoctorId = doctor.Id,
            Status = "Called",
            PriorityScore = 100,
            BaseWeight = 100,
            BookedAtUtc = DateTime.UtcNow,
            CalledAtUtc = DateTime.UtcNow,
            ConsultationNotes = null // Notes NOT finalized
        };

        // Patient 2 waiting
        var waitingToken = new QueueToken
        {
            RowVersion = new byte[] { 1, 0, 0, 0 },
            Id = Guid.NewGuid(),
            TokenNumber = "CARD-002",
            PatientId = patient.Id,
            DepartmentId = dept.Id,
            DoctorId = doctor.Id,
            Status = "Waiting",
            PriorityScore = 500,
            BaseWeight = 500,
            BookedAtUtc = DateTime.UtcNow
        };

        await context.QueueTokens.AddRangeAsync(activeToken, waitingToken);
        await context.SaveChangesAsync();

        var service = new DoctorService(context);

        // Act: Doctor attempts to call next while activeToken is still in room
        var act = async () => await service.CallNextPatientAsync(doctor.Id);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*while Token #CARD-001 is currently in Room*");
    }

    [Fact]
    public async Task CallNextPatientAsync_PicksHighestPriorityScoreFirst()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var (dept, doctor, doctorUser, patient) = await TestDbContextFactory.SeedTestClinicAsync(context);

        var routineGreenToken = new QueueToken
        {
            RowVersion = new byte[] { 1, 0, 0, 0 },
            Id = Guid.NewGuid(),
            TokenNumber = "CARD-001",
            PatientId = patient.Id,
            DepartmentId = dept.Id,
            DoctorId = doctor.Id,
            Status = "Waiting",
            PriorityScore = 150,
            BaseWeight = 100,
            BookedAtUtc = DateTime.UtcNow.AddMinutes(-30)
        };

        var urgentYellowToken = new QueueToken
        {
            RowVersion = new byte[] { 1, 0, 0, 0 },
            Id = Guid.NewGuid(),
            TokenNumber = "CARD-002",
            PatientId = patient.Id,
            DepartmentId = dept.Id,
            DoctorId = doctor.Id,
            Status = "Waiting",
            PriorityScore = 520, // Higher priority score
            BaseWeight = 500,
            BookedAtUtc = DateTime.UtcNow.AddMinutes(-10)
        };

        await context.QueueTokens.AddRangeAsync(routineGreenToken, urgentYellowToken);
        await context.SaveChangesAsync();

        var service = new DoctorService(context);

        // Act
        var called = await service.CallNextPatientAsync(doctor.Id);

        // Assert: Higher priority score (CARD-002) is called first!
        called.Should().NotBeNull();
        called!.TokenNumber.Should().Be("CARD-002");
        called.Status.Should().Be("Called");
    }

    [Fact]
    public async Task SkipPatientAsync_ThreeStrikesProgression_TransitionsToPermanentNoShow()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var (dept, doctor, doctorUser, patient) = await TestDbContextFactory.SeedTestClinicAsync(context);

        var token = new QueueToken
        {
            RowVersion = new byte[] { 1, 0, 0, 0 },
            Id = Guid.NewGuid(),
            TokenNumber = "CARD-001",
            PatientId = patient.Id,
            DepartmentId = dept.Id,
            DoctorId = doctor.Id,
            Status = "Called",
            PriorityScore = 100,
            BaseWeight = 100,
            BookedAtUtc = DateTime.UtcNow,
            CalledAtUtc = DateTime.UtcNow
        };
        await context.QueueTokens.AddAsync(token);
        await context.SaveChangesAsync();

        var service = new DoctorService(context);

        // Act & Assert 1: Strike 1
        var strike1 = await service.SkipPatientAsync(token.Id, doctorUser.Id);
        strike1.Status.Should().Be("Skipped");
        strike1.ConsultationNotes.Should().Contain("Call 1 of 3");
        strike1.SkipCount.Should().Be(1);

        // Resummon for call 2
        token.Status = "Called";
        await context.SaveChangesAsync();

        // Act & Assert 2: Strike 2
        var strike2 = await service.SkipPatientAsync(token.Id, doctorUser.Id);
        strike2.Status.Should().Be("Skipped");
        strike2.ConsultationNotes.Should().Contain("Call 2 of 3");
        strike2.SkipCount.Should().Be(2);

        // Resummon for call 3
        token.Status = "Called";
        await context.SaveChangesAsync();

        // Act & Assert 3: Strike 3 -> Permanent No-Show / Expired!
        var strike3 = await service.SkipPatientAsync(token.Id, doctorUser.Id);
        strike3.Status.Should().Be("Expired");
        strike3.ConsultationNotes.Should().Contain("Absent after 3 calls");
        strike3.SkipCount.Should().Be(3);
        strike3.CompletedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task ResummonPatientAsync_CalledPatient_ReturnsToCalledStatus()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var (dept, doctor, doctorUser, patient) = await TestDbContextFactory.SeedTestClinicAsync(context);

        var token = new QueueToken
        {
            RowVersion = new byte[] { 1, 0, 0, 0 },
            Id = Guid.NewGuid(),
            TokenNumber = "CARD-001",
            PatientId = patient.Id,
            DepartmentId = dept.Id,
            DoctorId = doctor.Id,
            Status = "Called",
            PriorityScore = 100,
            BaseWeight = 100,
            BookedAtUtc = DateTime.UtcNow,
            CalledAtUtc = DateTime.UtcNow.AddMinutes(-2)
        };
        await context.QueueTokens.AddAsync(token);
        await context.SaveChangesAsync();

        var service = new DoctorService(context);

        // Act
        var resummoned = await service.ResummonPatientAsync(token.Id, doctor.Id);

        // Assert
        resummoned.Should().NotBeNull();
        resummoned!.Status.Should().Be("Called");
        resummoned.TokenNumber.Should().Be("CARD-001");
    }

    [Fact]
    public async Task RequeuePatientAsync_SkippedPatient_RequeuesBackToWaitingWithStrikePreserved()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var (dept, doctor, doctorUser, patient) = await TestDbContextFactory.SeedTestClinicAsync(context);

        var token = new QueueToken
        {
            RowVersion = new byte[] { 1, 0, 0, 0 },
            Id = Guid.NewGuid(),
            TokenNumber = "CARD-001",
            PatientId = patient.Id,
            DepartmentId = dept.Id,
            DoctorId = doctor.Id,
            Status = "Skipped",
            PriorityScore = 100,
            BaseWeight = 100,
            BookedAtUtc = DateTime.UtcNow,
            CalledAtUtc = DateTime.UtcNow.AddMinutes(-5),
            ConsultationNotes = "[Skipped: Call 1 of 3]"
        };
        await context.QueueTokens.AddAsync(token);
        await context.SaveChangesAsync();

        var service = new DoctorService(context);

        // Act
        var requeued = await service.RequeuePatientAsync(token.Id, doctorUser.Id);

        // Assert
        requeued.Should().NotBeNull();
        requeued.Status.Should().Be("Waiting");
        requeued.SkipCount.Should().Be(1);
    }

    [Fact]
    public async Task RecalculateQueueScoresAsync_WaitingTokens_UpdatesDynamicAgingScores()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var (dept, doctor, doctorUser, patient) = await TestDbContextFactory.SeedTestClinicAsync(context);

        var token = new QueueToken
        {
            RowVersion = new byte[] { 1, 0, 0, 0 },
            Id = Guid.NewGuid(),
            TokenNumber = "CARD-001",
            PatientId = patient.Id,
            DepartmentId = dept.Id,
            DoctorId = doctor.Id,
            Status = "Waiting",
            PriorityScore = 100, // Initial base
            BaseWeight = 100,
            BookedAtUtc = DateTime.UtcNow.AddMinutes(-20),
            TriagedAtUtc = DateTime.UtcNow.AddMinutes(-20) // 20 mins wait = 100 + 40 = 140
        };
        await context.QueueTokens.AddAsync(token);
        await context.SaveChangesAsync();

        var service = new DoctorService(context);

        // Act
        var hasChanges = await service.RecalculateQueueScoresAsync();

        // Assert
        hasChanges.Should().BeTrue();
        var updated = await context.QueueTokens.FindAsync(token.Id);
        updated!.PriorityScore.Should().Be(140);
    }
}
