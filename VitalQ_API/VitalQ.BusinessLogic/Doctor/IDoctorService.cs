using VitalQ.Entities.DTOs;

namespace VitalQ.BusinessLogic.Interfaces;

/// <summary>
/// Dedicated service for the Doctor Consultation Room on PC.
/// Handles doctor queue management, calling next patient, skipping, requeueing, and consultation completion.
/// </summary>
public interface IDoctorService
{
    /// <summary>
    /// Retrieves the doctor clinical profile linked to an authenticated user ID.
    /// </summary>
    Task<DoctorResponse?> GetDoctorByUserIdAsync(Guid userId);

    /// <summary>
    /// Retrieves the live priority-sorted queue for a doctor's consultation room.
    /// </summary>
    Task<IEnumerable<QueueTokenResponse>> GetMyQueueAsync(Guid doctorId);

    /// <summary>
    /// Retrieves the patient currently called into the doctor's consultation room.
    /// </summary>
    Task<QueueTokenResponse?> GetCurrentCalledPatientAsync(Guid doctorId);

    /// <summary>
    /// Calls the next highest-priority patient into the consultation room.
    /// </summary>
    Task<QueueTokenResponse?> CallNextPatientAsync(Guid doctorId);

    /// <summary>
    /// Marks a called patient as skipped if they are absent.
    /// </summary>
    Task<QueueTokenResponse> SkipPatientAsync(Guid tokenId, Guid doctorUserId);

    /// <summary>
    /// Returns a skipped patient back to the active waiting queue.
    /// </summary>
    Task<QueueTokenResponse> RequeuePatientAsync(Guid tokenId, Guid doctorUserId);

    /// <summary>
    /// Completes the consultation with clinical diagnosis and prescription notes.
    /// </summary>
    Task<QueueTokenResponse> CompleteConsultationAsync(Guid tokenId, Guid doctorUserId, CompleteConsultationRequest request);

    /// <summary>
    /// Recalculates dynamic wait-time priority scores across all waiting tokens.
    /// </summary>
    Task<bool> RecalculateQueueScoresAsync();

    /// <summary>
    /// Sweeps and marks unvisited stale tokens from past days as expired.
    /// </summary>
    Task<int> ExpireStaleTokensAsync();
}
