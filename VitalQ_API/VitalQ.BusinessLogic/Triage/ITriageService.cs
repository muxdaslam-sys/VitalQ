using VitalQ.Entities.DTOs;

namespace VitalQ.BusinessLogic.Interfaces;

public interface ITriageService
{
    Task<QueueTokenResponse> RecordTriageAsync(Guid tokenId, Guid nurseUserId, TriageRequest request);
    Task<IEnumerable<PatientSearchResult>> SearchPatientsAsync(string query);
    Task<IEnumerable<QueueTokenResponse>> GetPendingTriageTokensAsync(Guid? departmentId = null);
    Task<TriageAssessmentResponse?> GetTriageAssessmentByTokenIdAsync(Guid tokenId);
    string CalculateAutomatedTriageLevel(decimal spo2, int systolicBp, int heartRate, decimal temperature, int painScale);
    int CalculateBaseWeight(string triageLevel);
}
