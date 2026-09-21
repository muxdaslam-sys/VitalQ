using VitalQ.BusinessLogic.Interfaces;
using VitalQ.DataAccess.Repositories;
using VitalQ.Entities.DTOs;

namespace VitalQ.BusinessLogic.Services;

public class TriageService : ITriageService
{
    private readonly IQueueTokenRepository _tokenRepo;
    private readonly IPatientRepository _patientRepo;

    public TriageService(IQueueTokenRepository tokenRepo, IPatientRepository patientRepo)
    {
        _tokenRepo = tokenRepo;
        _patientRepo = patientRepo;
    }

    public Task<QueueTokenResponse> RecordTriageAsync(Guid tokenId, Guid nurseUserId, TriageRequest request)
    {
        throw new NotImplementedException();
    }

    public Task<IEnumerable<PatientSearchResult>> SearchPatientsAsync(string query)
    {
        throw new NotImplementedException();
    }

    public string CalculateAutomatedTriageLevel(decimal spo2, int systolicBp, int heartRate, decimal temperature, int painScale)
    {
        // Clinical vital sign thresholds (§05 Automated triage)
        if (spo2 < 90 || systolicBp < 90 || heartRate > 130 || heartRate < 40 || temperature > 39.5m || painScale >= 8)
        {
            return "Red";    // Emergency
        }

        if (spo2 <= 94 || systolicBp >= 160 || heartRate >= 100 || temperature >= 38.0m || painScale >= 5)
        {
            return "Yellow"; // Urgent
        }

        return "Green";     // Routine
    }

    public int CalculateBaseWeight(string triageLevel)
    {
        return triageLevel switch
        {
            "Red" => PriorityScoreCalculator.BaseWeightRed,
            "Yellow" => PriorityScoreCalculator.BaseWeightYellow,
            _ => PriorityScoreCalculator.BaseWeightGreen
        };
    }
}
