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

    #region 2. Clinical Triage & Vital Signs Intake
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

    #region 5. Dashboard KPIs & Live Doctor Queues
    /// <summary>
    /// Aggregates today's OPD metrics and doctor live queue roster for the station.
    /// </summary>
    Task<NurseDashboardStatsResponse> GetDashboardStatsAsync(Guid? nursingStationId);

    /// <summary>
    /// Retrieves full live queue for a specific doctor with priority scores and triage vitals.
    /// </summary>
    Task<DoctorDetailedQueueResponse> GetDoctorQueueAsync(Guid doctorId);

    /// <summary>
    /// Retrieves all active tokens booked today for a patient.
    /// </summary>
    Task<IEnumerable<QueueTokenResponse>> GetPatientActiveTokensAsync(Guid patientId);

    /// <summary>
    /// Retrieves complete patient medical profile and visit history for the Full Details modal.
    /// </summary>
    Task<PatientDetailResponse> GetPatientDetailsAsync(Guid patientId);

    /// <summary>
    /// Computes real-time line position, wait estimates, and doctor queue visualizer for a patient token.
    /// </summary>
    Task<PatientQueuePositionResponse> GetPatientQueuePositionAsync(Guid tokenId);

    /// <summary>
    /// Registers or resolves existing patient, generates walk-in token, and optionally executes Code-Red emergency bypass.
    /// </summary>
    Task<QueueTokenResponse> ProcessEmergencyWalkInAsync(EmergencyWalkInBookingRequest request, Guid nurseUserId, Guid? nursingStationId);
    #endregion
}
