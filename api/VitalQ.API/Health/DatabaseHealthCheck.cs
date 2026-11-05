using System.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using VitalQ.DataAccess;

namespace VitalQ.API.Health;

/// <summary>
/// Probes SQL Server database connectivity and measures query roundtrip latency.
/// Reports "Healthy" (HTTP 200) if reachable, or "Unhealthy" (HTTP 503) if disconnected.
/// </summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly VitalQDbContext _context;

    public DatabaseHealthCheck(VitalQDbContext context)
    {
        _context = context;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Executes an optimized, non-blocking connection ping against SQL Server
            var canConnect = await _context.Database.CanConnectAsync(cancellationToken);
            stopwatch.Stop();

            if (canConnect)
            {
                var latencyMs = stopwatch.ElapsedMilliseconds;
                return HealthCheckResult.Healthy(
                    description: $"SQL Server connected successfully. Roundtrip latency: {latencyMs}ms.",
                    data: new Dictionary<string, object>
                    {
                        { "latencyMs", latencyMs },
                        { "database", _context.Database.ProviderName ?? "SqlServer" }
                    });
            }

            return HealthCheckResult.Unhealthy("SQL Server connection refused or database offline.");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return HealthCheckResult.Unhealthy(
                description: $"SQL Server connection failed: {ex.Message}",
                exception: ex);
        }
    }
}
