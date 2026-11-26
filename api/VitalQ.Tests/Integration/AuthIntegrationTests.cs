using System.Net;
using System.Text;
using System.Text.Json;
using VitalQ.Entities.DTOs;

namespace VitalQ.Tests.Integration;

public class AuthIntegrationTests : IClassFixture<VitalQWebApplicationFactory>
{
    private readonly VitalQWebApplicationFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public AuthIntegrationTests(VitalQWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_ValidAdminCredentials_Returns200WithJwtTokenAndAdminRole()
    {
        // Arrange
        var client = _factory.CreateClient();
        var payload = new LoginRequest
        {
            Username = "admin.test",
            Password = "AdminPass123!"
        };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/auth/login", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var authResult = JsonSerializer.Deserialize<AuthResponse>(json, _jsonOptions);

        authResult.Should().NotBeNull();
        authResult!.AccessToken.Should().NotBeNullOrWhiteSpace();
        authResult.User.Username.Should().Be("admin.test");
        authResult.User.Role.Should().Be("Admin");
        authResult.ExpiresAtUtc.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task Login_InvalidPassword_Returns401Unauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        var payload = new LoginRequest
        {
            Username = "admin.test",
            Password = "WrongPassword123!"
        };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/auth/login", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdminUsers_UnauthenticatedRequest_Returns401Unauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/admin/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdminUsers_NurseRoleCaller_Returns403Forbidden()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync("nurse.jane", "NursePass123!");

        // Act
        var response = await client.GetAsync("/api/admin/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DoctorMe_DoctorRoleCaller_Returns200WithDoctorProfile()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync("dr.smith", "DoctorPass123!");

        // Act
        var response = await client.GetAsync("/api/doctor/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doctor = JsonSerializer.Deserialize<DoctorResponse>(json, _jsonOptions);

        doctor.Should().NotBeNull();
        doctor!.DoctorName.Should().Be("Dr. Sarah Smith");
        doctor.Specialization.Should().Be("Cardiologist");
        doctor.RoomNumber.Should().Be("201");
    }
}
