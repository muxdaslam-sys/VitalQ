using VitalQ.BusinessLogic.Interfaces;
using VitalQ.BusinessLogic.Services;
using VitalQ.Entities.DTOs;
using VitalQ.Entities.Models;
using VitalQ.Tests.Helpers;
using PatientEntity = VitalQ.Entities.Models.Patient;

namespace VitalQ.Tests.Patient;

public class PatientServiceTests
{
    [Fact]
    public async Task SelfRegisterAsync_FutureDateOfBirth_ThrowsArgumentException()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var mockAuth = new Mock<IAuthService>();
        var service = new PatientService(context, mockAuth.Object);

        var request = new SelfRegisterRequest
        {
            FullName = "Baby Future",
            PhoneNumber = "1234567890",
            DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), // Future!
            Gender = "Female"
        };

        // Act
        var act = async () => await service.SelfRegisterAsync(request);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*must be in the past*");
    }

    [Fact]
    public async Task SelfRegisterAsync_DuplicatePhoneNumber_ThrowsInvalidOperationException()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var existingUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "9876543210",
            Password = "Password123",
            FullName = "Existing User",
            PhoneNumber = "9876543210",
            Role = "Patient",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        await context.Users.AddAsync(existingUser);
        await context.SaveChangesAsync();

        var mockAuth = new Mock<IAuthService>();
        var service = new PatientService(context, mockAuth.Object);

        var request = new SelfRegisterRequest
        {
            FullName = "New Person Same Phone",
            PhoneNumber = "9876543210",
            DateOfBirth = new DateOnly(1995, 1, 1),
            Gender = "Male"
        };

        // Act
        var act = async () => await service.SelfRegisterAsync(request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*account with this phone number already exists*");
    }

    [Fact]
    public async Task CancelMyTokenAsync_NotOwner_ThrowsUnauthorizedAccessException()
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
            Status = "Booked",
            BookedAtUtc = DateTime.UtcNow
        };
        await context.QueueTokens.AddAsync(token);
        await context.SaveChangesAsync();

        var otherUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "other.patient",
            Password = "hash",
            FullName = "Other Patient",
            PhoneNumber = "0000000000",
            Role = "Patient",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        await context.Users.AddAsync(otherUser);
        await context.SaveChangesAsync();

        var mockAuth = new Mock<IAuthService>();
        var service = new PatientService(context, mockAuth.Object);

        // Act: Other user tries to cancel someone else's token
        var act = async () => await service.CancelMyTokenAsync(token.Id, otherUser.Id, "Cannot make it");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*not authorized to cancel this token*");
    }

    [Fact]
    public async Task CancelMyTokenAsync_AlreadyCompleted_ThrowsInvalidOperationException()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var (dept, doctor, doctorUser, patient) = await TestDbContextFactory.SeedTestClinicAsync(context);

        var patientUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "9998887777",
            Password = "hash",
            FullName = patient.FullName,
            PhoneNumber = patient.PhoneNumber,
            Role = "Patient",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        patient.UserId = patientUser.Id;
        await context.Users.AddAsync(patientUser);

        var token = new QueueToken
        {
            RowVersion = new byte[] { 1, 0, 0, 0 },
            Id = Guid.NewGuid(),
            TokenNumber = "CARD-001",
            PatientId = patient.Id,
            DepartmentId = dept.Id,
            DoctorId = doctor.Id,
            Status = "Completed", // Already completed
            BookedAtUtc = DateTime.UtcNow,
            CompletedAtUtc = DateTime.UtcNow
        };
        await context.QueueTokens.AddAsync(token);
        await context.SaveChangesAsync();

        var mockAuth = new Mock<IAuthService>();
        var service = new PatientService(context, mockAuth.Object);

        // Act
        var act = async () => await service.CancelMyTokenAsync(token.Id, patientUser.Id, "Cancel please");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot cancel a consultation that has already been completed*");
    }
}
