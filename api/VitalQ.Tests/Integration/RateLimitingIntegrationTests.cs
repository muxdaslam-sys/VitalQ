using System.Net;
using System.Text;
using System.Text.Json;
using VitalQ.Entities.DTOs;

namespace VitalQ.Tests.Integration;

public class RateLimitingIntegrationTests : IClassFixture<VitalQWebApplicationFactory>
{
    private readonly VitalQWebApplicationFactory _factory;

    public RateLimitingIntegrationTests(VitalQWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task LoginEndpoint_Exceeding10RequestsPerMinute_Returns429TooManyRequests()
    {
        // Arrange
        var client = _factory.CreateClient();
        var testIp = "198.51.100.99";
        client.DefaultRequestHeaders.Add("X-Forwarded-For", testIp);

        var payload = new LoginRequest
        {
            Username = "admin.test",
            Password = "InvalidPasswordToTestRateLimit"
        };
        var contentString = JsonSerializer.Serialize(payload);

        // Act: Execute 10 rapid requests (PermitLimit = 10)
        for (int i = 1; i <= 10; i++)
        {
            var content = new StringContent(contentString, Encoding.UTF8, "application/json");
            var res = await client.PostAsync("/api/auth/login", content);
            res.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
                $"Request #{i} within the 10 req/min quota should reach the controller and return 401.");
        }

        // Act: 11th request exceeds the limit
        var burstContent = new StringContent(contentString, Encoding.UTF8, "application/json");
        var burstResponse = await client.PostAsync("/api/auth/login", burstContent);

        // Assert: 11th request is throttled
        burstResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests,
            "Request #11 exceeding 10 req/min must be throttled with HTTP 429 Too Many Requests.");

        // Assert: A different client IP is NOT throttled (validating IP-partitioned rate limiter)
        var differentClient = _factory.CreateClient();
        differentClient.DefaultRequestHeaders.Add("X-Forwarded-For", "198.51.100.100");
        var separateContent = new StringContent(contentString, Encoding.UTF8, "application/json");
        var separateResponse = await differentClient.PostAsync("/api/auth/login", separateContent);

        separateResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "A request from a distinct IP must not be throttled by another partition's rate limit exhaustion.");
    }
}
