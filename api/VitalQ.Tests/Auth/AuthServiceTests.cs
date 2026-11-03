using Microsoft.Extensions.Configuration;
using VitalQ.BusinessLogic.Services;
using VitalQ.Entities.DTOs;
using VitalQ.Entities.Models;
using VitalQ.Tests.Helpers;

namespace VitalQ.Tests.Auth;

public class AuthServiceTests
{
    private static IConfiguration CreateTestConfig()
    {
        var configValues = new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "SuperSecretSecureKeyForVitalQUnitTesting12345!",
            ["Jwt:DurationInMinutes"] = "15",
            ["Jwt:Issuer"] = "VitalQTest",
            ["Jwt:Audience"] = "VitalQTest"
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsJwtAndUserDetails()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "doctor.test",
            Password = "Password123",
            FullName = "Dr. Test",
            Role = "Doctor",
            PhoneNumber = "1234567890",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();

        var config = CreateTestConfig();
        var authService = new AuthService(context, config);

        // Act
        var result = await authService.LoginAsync(new LoginRequest
        {
            Username = "doctor.test",
            Password = "Password123"
        });

        // Assert
        result.Should().NotBeNull();
        result!.AccessToken.Should().NotBeNullOrWhiteSpace();
        result.User.Username.Should().Be("doctor.test");
        result.User.Role.Should().Be("Doctor");
        result.User.FullName.Should().Be("Dr. Test");
        result.ExpiresAtUtc.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ReturnsNull()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "doctor.test",
            Password = "Password123",
            FullName = "Dr. Test",
            Role = "Doctor",
            PhoneNumber = "1234567890",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();

        var config = CreateTestConfig();
        var authService = new AuthService(context, config);

        // Act
        var result = await authService.LoginAsync(new LoginRequest
        {
            Username = "doctor.test",
            Password = "WrongPassword!"
        });

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task LoginAsync_InactiveUser_ReturnsNull()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "deactivated.nurse",
            Password = "Password123",
            FullName = "Nurse Jane",
            Role = "Nurse",
            PhoneNumber = "1234567890",
            IsActive = false, // Deactivated!
            CreatedAtUtc = DateTime.UtcNow
        };
        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();

        var config = CreateTestConfig();
        var authService = new AuthService(context, config);

        // Act
        var result = await authService.LoginAsync(new LoginRequest
        {
            Username = "deactivated.nurse",
            Password = "Password123"
        });

        // Assert
        result.Should().BeNull();
    }
}
