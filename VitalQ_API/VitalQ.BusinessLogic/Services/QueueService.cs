using VitalQ.BusinessLogic.Interfaces;
using VitalQ.DataAccess.Repositories;
using VitalQ.Entities.DTOs;

namespace VitalQ.BusinessLogic.Services;

public class QueueService : IQueueService
{
    private readonly IQueueTokenRepository _tokenRepo;

    public QueueService(IQueueTokenRepository tokenRepo)
    {
        _tokenRepo = tokenRepo;
    }

    public Task<IEnumerable<QueueTokenResponse>> GetDoctorQueueAsync(Guid doctorId)
    {
        throw new NotImplementedException();
    }

    public Task<QueueTokenResponse?> CallNextPatientAsync(Guid doctorId)
    {
        throw new NotImplementedException();
    }

    public Task<QueueTokenResponse> SkipPatientAsync(Guid tokenId, Guid doctorUserId)
    {
        throw new NotImplementedException();
    }

    public Task<QueueTokenResponse> CompleteConsultationAsync(Guid tokenId, Guid doctorUserId, CompleteConsultationRequest request)
    {
        throw new NotImplementedException();
    }

    public Task RecalculateQueueScoresAsync()
    {
        throw new NotImplementedException();
    }
}
