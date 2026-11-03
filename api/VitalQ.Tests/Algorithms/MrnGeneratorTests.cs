using System.Text.RegularExpressions;
using FluentAssertions;
using VitalQ.BusinessLogic.Common;
using VitalQ.Tests.Helpers;
using Xunit;
using PatientEntity = VitalQ.Entities.Models.Patient;

namespace VitalQ.Tests.Algorithms;

public class MrnGeneratorTests
{
    [Fact]
    public async Task GenerateMrnAsync_EmptyDatabase_StartsAt000001()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();

        // Act
        var mrn = await MrnGenerator.GenerateMrnAsync(context);

        // Assert
        mrn.Should().NotBeNullOrWhiteSpace();
        Regex.IsMatch(mrn, @"^MRN-\d{6}$").Should().BeTrue($"MRN was: {mrn}");
        mrn.Should().Be("MRN-000001");
        mrn.Length.Should().Be(10);
    }

    [Fact]
    public async Task GenerateMrnAsync_IncrementsSequentiallyFromExistingHighestPatient()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        await context.Patients.AddAsync(new PatientEntity
        {
            Id = Guid.NewGuid(),
            MedicalRecordNumber = "MRN-000042",
            FullName = "Test Patient",
            PhoneNumber = "1112223333",
            Gender = "Male",
            DateOfBirth = new DateOnly(1995, 1, 1),
            CreatedAtUtc = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        // Act
        var nextMrn = await MrnGenerator.GenerateMrnAsync(context);

        // Assert
        nextMrn.Should().Be("MRN-000043");
    }

    [Fact]
    public async Task GenerateMrnAsync_DynamicallyScalesPastMillionGracefully()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        await context.Patients.AddAsync(new PatientEntity
        {
            Id = Guid.NewGuid(),
            MedicalRecordNumber = "MRN-999999",
            FullName = "One Millionth Candidate",
            PhoneNumber = "2223334444",
            Gender = "Female",
            DateOfBirth = new DateOnly(1990, 6, 20),
            CreatedAtUtc = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        // Act
        var nextMrn = await MrnGenerator.GenerateMrnAsync(context);

        // Assert: Dynamic expansion past 6 digits without truncating (11 chars, well under 30 DB limit)
        nextMrn.Should().Be("MRN-1000000");
        nextMrn.Length.Should().Be(11);
    }

    [Fact]
    public async Task GenerateMrnAsync_PreventsCollisionsEvenIfCandidateTaken()
    {
        // Arrange: Insert 000001 and 000002
        using var context = TestDbContextFactory.Create();
        await context.Patients.AddRangeAsync(
            new PatientEntity
            {
                Id = Guid.NewGuid(),
                MedicalRecordNumber = "MRN-000001",
                FullName = "Patient 1",
                PhoneNumber = "1234567890",
                Gender = "Male",
                DateOfBirth = new DateOnly(1980, 1, 1),
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5)
            },
            new PatientEntity
            {
                Id = Guid.NewGuid(),
                MedicalRecordNumber = "MRN-000002",
                FullName = "Patient 2",
                PhoneNumber = "0987654321",
                Gender = "Female",
                DateOfBirth = new DateOnly(1985, 2, 2),
                CreatedAtUtc = DateTime.UtcNow
            }
        );
        await context.SaveChangesAsync();

        // Act
        var nextMrn = await MrnGenerator.GenerateMrnAsync(context);

        // Assert
        nextMrn.Should().Be("MRN-000003");
    }

    [Fact]
    public async Task GenerateMrnAsync_IgnoresLegacyHyphenatedFormatAndStartsCleanSequence()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        await context.Patients.AddAsync(new PatientEntity
        {
            Id = Guid.NewGuid(),
            MedicalRecordNumber = "MRN-261103-7A3F9B2C",
            FullName = "Legacy Patient",
            PhoneNumber = "5554443322",
            Gender = "Female",
            DateOfBirth = new DateOnly(1992, 4, 12),
            CreatedAtUtc = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        // Act
        var mrn = await MrnGenerator.GenerateMrnAsync(context);

        // Assert: Safely ignores legacy date formats and starts clean sequence at 000001
        mrn.Should().Be("MRN-000001");
    }
}
