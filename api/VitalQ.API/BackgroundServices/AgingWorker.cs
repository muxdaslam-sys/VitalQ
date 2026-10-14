using Microsoft.AspNetCore.SignalR;
using VitalQ.API.Hubs;
using VitalQ.BusinessLogic.Interfaces;

namespace VitalQ.API.BackgroundServices;

/// <summary>
/// Periodic background service running every 60 seconds to expire stale tokens, recalculate priority scores, and broadcast SignalR updates.
/// </summary>
public class AgingWorker : BackgroundService
{
    private readonly ILogger<AgingWorker> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IHubContext<QueueHub> _hubContext;

    public AgingWorker(
        ILogger<AgingWorker> logger,
        IServiceProvider serviceProvider,
        IHubContext<QueueHub> hubContext)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _hubContext = hubContext;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AgingWorker background service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);

                using (var scope = _serviceProvider.CreateScope())
                {
                    var doctorService = scope.ServiceProvider.GetRequiredService<IDoctorService>();

                    // 1. Auto-sweep stale unvisited tokens from previous days
                    await doctorService.ExpireStaleTokensAsync();

                    // 2. Recalculate priority scores for waiting patients
                    var hasChanges = await doctorService.RecalculateQueueScoresAsync();

                    // 3. Only broadcast SignalR if scores actually changed (prevents broadcast spam)
                    if (hasChanges)
                    {
                        await _hubContext.Clients.All.SendAsync("QueueScoresRecalculated", DateTime.UtcNow, cancellationToken: stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during queue aging recalculation.");
            }
        }

        _logger.LogInformation("AgingWorker background service stopping.");
    }
}
