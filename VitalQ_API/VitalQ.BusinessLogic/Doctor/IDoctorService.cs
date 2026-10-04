using VitalQ.Entities.DTOs;

namespace VitalQ.BusinessLogic.Interfaces;

/// <summary>
/// Dedicated service for the Doctor Consultation Room on PC.
/// Handles doctor queue management, calling next patient, skipping, requeueing, and consultation completion.
/// </summary>
public interface IDoctorService
{
    // 1. Get doctor profile for the authenticated doctor user
    Task<DoctorResponse?> GetDoctorByUserIdAsync(Guid userId);

    // 2. Get live priority-sorted queue for the doctor's screen
    Task<IEnumerable<QueueTokenResponse>> GetMyQueueAsync(Guid doctorId);

    // 3. Get the patient currently inside the consultation room
    Task<QueueTokenResponse?> GetCurrentCalledPatientAsync(Guid doctorId);

    // 4. Call next patient into the room (Guarded by optimistic concurrency)
    Task<QueueTokenResponse?> CallNextPatientAsync(Guid doctorId);

    // 5. Skip patient if they do not enter the room
    Task<QueueTokenResponse> SkipPatientAsync(Guid tokenId, Guid doctorUserId);

    // 6. Return skipped patient back to active queue when they arrive
    Task<QueueTokenResponse> RequeuePatientAsync(Guid tokenId, Guid doctorUserId);

    // 7. Complete consultation with notes and diagnosis
    Task<QueueTokenResponse> CompleteConsultationAsync(Guid tokenId, Guid doctorUserId, CompleteConsultationRequest request);

    // 8. Background engine recalculation for priority aging
    Task RecalculateQueueScoresAsync();
}
