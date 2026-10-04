using VitalQ.Entities.DTOs;

namespace VitalQ.BusinessLogic.Interfaces;

/// <summary>
/// Dedicated service for the Nurse Station / Reception Counter on PC.
/// Handles walk-in patient registration, searching, counter token booking, and clinical vitals triage.
/// </summary>
public interface INurseService
{
    // 1. Search patients by Phone, MRN, or Name with active token status
    Task<IEnumerable<PatientSearchResult>> SearchPatientsAsync(string query);

    // 2. Register a walk-in patient arriving in person at the clinic desk
    Task<PatientResponse> RegisterWalkInPatientAsync(WalkInRegisterRequest request);

    // 3. Issue a walk-in token at the desk counter
    Task<QueueTokenResponse> BookWalkInTokenAsync(BookTokenRequest request);

    // 4. View queue of patients booked today who need vital signs taken
    Task<IEnumerable<QueueTokenResponse>> GetPendingTriageQueueAsync(Guid? departmentId = null);

    // 5. Record vital signs (BP, SpO2, HR, Temp, Pain) and assign CTAS urgency level
    Task<QueueTokenResponse> RecordVitalsAndTriageAsync(Guid tokenId, Guid nurseUserId, TriageRequest request);

    // 6. View recorded vitals and assessment details for a token
    Task<TriageAssessmentResponse?> GetTriageAssessmentAsync(Guid tokenId);
}
