using Microsoft.AspNetCore.SignalR;
using VitalQ.API.Hubs;
using VitalQ.BusinessLogic.Interfaces;

namespace VitalQ.API.BackgroundServices;

/// <summary>
/// Background worker running every 60 seconds (§08 Recalculation, Page 7 & 18).
/// Recalculates priority scores with aging formula and broadcasts live updates over SignalR.
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
                    var queueService = scope.ServiceProvider.GetRequiredService<IQueueService>();
                    await queueService.RecalculateQueueScoresAsync();
                }

                // Broadcast tick to all connected clients so active queues re-sort
                await _hubContext.Clients.All.SendAsync("QueueScoresRecalculated", DateTime.UtcNow, cancellationToken: stoppingToken);
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
