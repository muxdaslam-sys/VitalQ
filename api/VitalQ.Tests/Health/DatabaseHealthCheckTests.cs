using Microsoft.Extensions.Diagnostics.HealthChecks;
using VitalQ.API.Health;
using VitalQ.Tests.Helpers;

namespace VitalQ.Tests.Health;

public class DatabaseHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_WhenDatabaseIsReachable_ReturnsHealthy()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var healthCheck = new DatabaseHealthCheck(context);
        var checkContext = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(checkContext);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("SQL Server connected successfully");
        result.Data.Should().ContainKey("latencyMs");
    }
}
