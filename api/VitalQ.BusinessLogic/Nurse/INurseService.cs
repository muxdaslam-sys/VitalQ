using VitalQ.Entities.DTOs;

namespace VitalQ.BusinessLogic.Interfaces;

/// <summary>
/// Defines nurse station workflows: patient search, walk-in registration, token issuance, and clinical triage.
/// </summary>
public interface INurseService
{
    #region 1. Patient Lookup & Identification
    /// <summary>
    /// Searches patients by Token Number, MRN, Phone, or Name with today's active token status.
    /// </summary>
    Task<IEnumerable<PatientSearchResult>> SearchPatientsAsync(string query);
    #endregion

    #region 2. Walk-In Registration & Identity Linking
    /// <summary>
    /// Registers a walk-in patient arriving in person and issues an MRN + User credentials.
    /// </summary>
    Task<PatientResponse> RegisterWalkInPatientAsync(WalkInRegisterRequest request);
    #endregion

    #region 3. Reception Desk Token Issuance
    /// <summary>
    /// Issues an atomic walk-in token at the clinic counter.
    /// </summary>
    Task<QueueTokenResponse> BookWalkInTokenAsync(BookTokenRequest request);
    #endregion

    #region 4. Clinical Triage & Vital Signs Intake
    /// <summary>
    /// Lists patients booked today awaiting vital signs intake, filterable by Department or Station.
    /// </summary>
    Task<IEnumerable<QueueTokenResponse>> GetPendingTriageQueueAsync(Guid? departmentId = null, Guid? nursingStationId = null);

    /// <summary>
    /// Records 5 vital signs, verifies nursing station, calculates CTAS level, and activates token into waiting queue.
    /// </summary>
    Task<QueueTokenResponse> RecordVitalsAndTriageAsync(Guid tokenId, Guid nurseUserId, TriageRequest request);

    /// <summary>
    /// Retrieves recorded vital signs and triage assessment for a consultation token.
    /// </summary>
    Task<TriageAssessmentResponse?> GetTriageAssessmentAsync(Guid tokenId);
    #endregion
}
