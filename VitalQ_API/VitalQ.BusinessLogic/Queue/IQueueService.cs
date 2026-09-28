using VitalQ.Entities.DTOs;

namespace VitalQ.BusinessLogic.Interfaces;

public interface IQueueService
{
    Task<IEnumerable<QueueTokenResponse>> GetDoctorQueueAsync(Guid doctorId);
    Task<QueueTokenResponse?> CallNextPatientAsync(Guid doctorId);
    Task<QueueTokenResponse> SkipPatientAsync(Guid tokenId, Guid doctorUserId);
    Task<QueueTokenResponse> CompleteConsultationAsync(Guid tokenId, Guid doctorUserId, CompleteConsultationRequest request);
    Task RecalculateQueueScoresAsync();
}
