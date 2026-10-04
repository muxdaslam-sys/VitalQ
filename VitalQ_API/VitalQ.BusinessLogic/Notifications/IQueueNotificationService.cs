using VitalQ.Entities.DTOs;

namespace VitalQ.BusinessLogic.Interfaces;

/// <summary>
/// Real-time push notification service for PC and Mobile browser clients.
/// Decouples business logic from SignalR WebSocket mechanics.
/// </summary>
public interface IQueueNotificationService
{
    // Notify doctor's consultation PC & nurse's triage PC when an appointment is booked
    Task NotifyTokenBookedAsync(QueueTokenResponse token);

    // Notify patient's mobile/PC screen (sound chime + room) & doctor's room PC when called
    Task NotifyPatientCalledAsync(QueueTokenResponse token);

    // Notify doctor's room PC when a patient is skipped or requeued
    Task NotifyQueueUpdatedAsync(Guid doctorId, QueueTokenResponse? token = null);

    // Notify doctor's room PC when nurse finishes vitals and assigns CTAS level
    Task NotifyTriageCompletedAsync(QueueTokenResponse token);
}
