using Microsoft.Extensions.Caching.Memory;
using VitalQ.BusinessLogic.Services;
using VitalQ.Entities.Models;
using VitalQ.Tests.Helpers;

namespace VitalQ.Tests.Public;

public class PublicServiceTests
{
    [Fact]
    public async Task TrackTokenAsync_EmptyOrNullToken_ReturnsNull()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new PublicService(context, cache);

        // Act & Assert
        (await service.TrackTokenAsync("")).Should().BeNull();
        (await service.TrackTokenAsync("   ")).Should().BeNull();
    }

    [Fact]
    public async Task TrackTokenAsync_NonExistentToken_ReturnsNull()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new PublicService(context, cache);

        // Act
        var result = await service.TrackTokenAsync("INVALID-999");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task TrackTokenAsync_ActiveWaitingToken_ReturnsWayfindingAndPatientsAhead()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var (dept, doctor, doctorUser, patient) = await TestDbContextFactory.SeedTestClinicAsync(context);

        var token1 = new QueueToken
        {
            RowVersion = new byte[] { 1, 0, 0, 0 },
            Id = Guid.NewGuid(),
            TokenNumber = "CARD-001",
            PatientId = patient.Id,
            DepartmentId = dept.Id,
            DoctorId = doctor.Id,
            Status = "Waiting",
            PriorityScore = 500,
            BaseWeight = 500,
            BookedAtUtc = DateTime.UtcNow.AddMinutes(-10),
            TriagedAtUtc = DateTime.UtcNow.AddMinutes(-10)
        };

        var token2 = new QueueToken
        {
            RowVersion = new byte[] { 1, 0, 0, 0 },
            Id = Guid.NewGuid(),
            TokenNumber = "CARD-002",
            PatientId = patient.Id,
            DepartmentId = dept.Id,
            DoctorId = doctor.Id,
            Status = "Waiting",
            PriorityScore = 100, // Lower priority, so token1 is ahead
            BaseWeight = 100,
            BookedAtUtc = DateTime.UtcNow,
            TriagedAtUtc = DateTime.UtcNow
        };

        await context.QueueTokens.AddRangeAsync(token1, token2);
        await context.SaveChangesAsync();

        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new PublicService(context, cache);

        // Act: Track token2 (which is behind token1)
        var result = await service.TrackTokenAsync("CARD-002");

        // Assert
        result.Should().NotBeNull();
        result!.TokenNumber.Should().Be("CARD-002");
        result.Status.Should().Be("Waiting");
        result.PatientsAhead.Should().Be(1); // token1 is ahead
        result.EstimatedWaitMinutes.Should().Be(10); // 1 patient * 10 mins avg
        result.RoomNumber.Should().Be("201");
        result.NextStepInstruction.Should().Contain("Room 201");
    }

    [Fact]
    public async Task TrackTokenAsync_CompletedToken_ReturnsDoctorConsultationNotes()
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
            Status = "Completed",
            PriorityScore = 100,
            BaseWeight = 100,
            BookedAtUtc = DateTime.UtcNow.AddMinutes(-30),
            CompletedAtUtc = DateTime.UtcNow,
            ConsultationNotes = "Normal ECG recorded. Maintain low-sodium diet and hydrate."
        };
        await context.QueueTokens.AddAsync(token);
        await context.SaveChangesAsync();

        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new PublicService(context, cache);

        // Act
        var result = await service.TrackTokenAsync("CARD-001");

        // Assert
        result.Should().NotBeNull();
        result!.Status.Should().Be("Completed");
        result.ConsultationNotes.Should().Be("Normal ECG recorded. Maintain low-sodium diet and hydrate.");
        result.CompletedAtUtc.Should().NotBeNull();
    }
}
