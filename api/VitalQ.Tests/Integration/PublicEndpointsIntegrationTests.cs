using System.Net;
using System.Text.Json;
using VitalQ.Entities.DTOs;

namespace VitalQ.Tests.Integration;

public class PublicEndpointsIntegrationTests : IClassFixture<VitalQWebApplicationFactory>
{
    private readonly VitalQWebApplicationFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public PublicEndpointsIntegrationTests(VitalQWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task LivenessProbe_Returns200Healthy()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health/live");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ReadinessProbe_Returns200WithHealthyReport()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Healthy");
    }

    [Fact]
    public async Task PublicDepartments_ReturnsActiveDepartmentsList()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/public/departments");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var departments = JsonSerializer.Deserialize<List<DepartmentResponse>>(json, _jsonOptions);

        departments.Should().NotBeNull();
        departments!.Should().Contain(d => d.Code == "CARD" && d.Name == "Cardiology");
    }
}
