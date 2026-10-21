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
    /// Broadcasts to Doctor PC console, Nurse triage desk, and Admin when a token is booked.
    /// </summary>
    public async Task NotifyTokenBookedAsync(QueueTokenResponse token)
    {
        var doctorGroup = $"doctor-{token.DoctorId.ToString().ToLowerInvariant()}";
        await _hubContext.Clients.Group(doctorGroup).SendAsync("TokenBooked", token);
        await _hubContext.Clients.Group("nurse-station").SendAsync("TokenBooked", token);
        await _hubContext.Clients.Group("admin-dashboard").SendAsync("TokenBooked", token);
    }

    /// <summary>
    /// Broadcasts to Patient's mobile/PC screen (triggers chime audio + room alert)
    /// and updates Doctor console, waiting patients, and Admin.
    /// </summary>
    public async Task NotifyPatientCalledAsync(QueueTokenResponse token)
    {
        var patientGroup = $"patient-{token.Id.ToString().ToLowerInvariant()}";
        var doctorGroup = $"doctor-{token.DoctorId.ToString().ToLowerInvariant()}";

        // 1. Patient summoned receives chime audio & room alert
        await _hubContext.Clients.Group(patientGroup).SendAsync("PatientCalled", token);

        // 2. Doctor PC and all other patients waiting in this doctor's line receive QueueUpdated
        await _hubContext.Clients.Group(doctorGroup).SendAsync("QueueUpdated", token);
        await _hubContext.Clients.Group("admin-dashboard").SendAsync("QueueUpdated", token);
    }

    /// <summary>
    /// Broadcasts when queue order shifts (Skip, Expire, Re-queue, Complete, Cancel).
    /// Updates Doctor console, affected patient, and Admin.
    /// </summary>
    public async Task NotifyQueueUpdatedAsync(Guid doctorId, QueueTokenResponse? token = null)
    {
        var doctorGroup = $"doctor-{doctorId.ToString().ToLowerInvariant()}";
        await _hubContext.Clients.Group(doctorGroup).SendAsync("QueueUpdated", token);

        // Explicitly update the specific patient whose token changed state (Skipped, Completed, Expired)
        if (token != null)
        {
            var patientGroup = $"patient-{token.Id.ToString().ToLowerInvariant()}";
            await _hubContext.Clients.Group(patientGroup).SendAsync("QueueUpdated", token);
        }

        await _hubContext.Clients.Group("admin-dashboard").SendAsync("QueueUpdated", token);
    }

    /// <summary>
    /// Broadcasts when triage vitals are recorded.
    /// Updates Doctor queue, Nurse desk, Patient tracker, and Admin.
    /// </summary>
    public async Task NotifyTriageCompletedAsync(QueueTokenResponse token)
    {
        var doctorGroup = $"doctor-{token.DoctorId.ToString().ToLowerInvariant()}";
        var patientGroup = $"patient-{token.Id.ToString().ToLowerInvariant()}";

        await _hubContext.Clients.Group(doctorGroup).SendAsync("QueueUpdated", token);
        await _hubContext.Clients.Group("nurse-station").SendAsync("TriageCompleted", token);
        await _hubContext.Clients.Group("nurse-station").SendAsync("QueueUpdated", token);
        await _hubContext.Clients.Group(patientGroup).SendAsync("QueueUpdated", token);
        await _hubContext.Clients.Group("admin-dashboard").SendAsync("QueueUpdated", token);
    }

    /// <summary>
    /// Broadcasts doctor availability status updates to the entire clinic floor and admin.
    /// </summary>
    public async Task NotifyDoctorStatusChangedAsync(Guid doctorId, string status)
    {
        await _hubContext.Clients.All.SendAsync("DoctorStatusChanged", new { doctorId, status });
    }
}
