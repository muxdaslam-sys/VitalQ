using Microsoft.AspNetCore.SignalR;
using VitalQ.API.Hubs;
using VitalQ.BusinessLogic.Interfaces;
using VitalQ.Entities.DTOs;

namespace VitalQ.API.Services;

/// <summary>
/// Real-time push notification service for PC and Mobile browser clients.
/// Encapsulates all SignalR messaging away from controllers and business logic.
/// </summary>
public class QueueNotificationService : IQueueNotificationService
{
    private readonly IHubContext<QueueHub> _hubContext;

    public QueueNotificationService(IHubContext<QueueHub> hubContext)
    {
        _hubContext = hubContext;
    }

    /// <summary>
    /// Broadcasts to Doctor PC console and Nurse PC station when a token is booked.
    /// </summary>
    public async Task NotifyTokenBookedAsync(QueueTokenResponse token)
    {
        // 1. Send to Doctor's PC room console
        await _hubContext.Clients.Group($"doctor-{token.DoctorId}").SendAsync("TokenBooked", token);

        // 2. Send to Nurse triage desk PC
        await _hubContext.Clients.Group("nurse-station").SendAsync("TokenBooked", token);

        // 3. Send to Department group for backward compatibility
        await _hubContext.Clients.Group($"dept-{token.DepartmentId}").SendAsync("QueueUpdated", token);
    }

    /// <summary>
    /// Broadcasts to Patient's mobile/PC screen (triggers chime audio + room alert)
    /// and updates Doctor's PC console.
    /// </summary>
    public async Task NotifyPatientCalledAsync(QueueTokenResponse token)
    {
        // 1. Patient's mobile or PC screen
        await _hubContext.Clients.Group($"patient-{token.Id}").SendAsync("PatientCalled", token);

        // 2. Doctor's PC console
        await _hubContext.Clients.Group($"doctor-{token.DoctorId}").SendAsync("QueueUpdated", token);
    }

    /// <summary>
    /// Broadcasts to Doctor's PC console when queue order shifts (skip, requeue).
    /// </summary>
    public async Task NotifyQueueUpdatedAsync(Guid doctorId, QueueTokenResponse? token = null)
    {
        await _hubContext.Clients.Group($"doctor-{doctorId}").SendAsync("QueueUpdated", token);
    }

    /// <summary>
    /// Broadcasts to Doctor's PC console and Nurse desk when triage is completed.
    /// </summary>
    public async Task NotifyTriageCompletedAsync(QueueTokenResponse token)
    {
        // 1. Doctor's PC queue now shows updated CTAS badge and priority score
        await _hubContext.Clients.Group($"doctor-{token.DoctorId}").SendAsync("QueueUpdated", token);

        // 2. Nurse desk marks pending item as completed
        await _hubContext.Clients.Group("nurse-station").SendAsync("TriageCompleted", token);
    }
}
