using VitalQ.Entities.DTOs;

namespace VitalQ.BusinessLogic.Interfaces;

/// <summary>
/// CONTRACT: Patient Portal Service
/// 
/// PURPOSE:
/// Defines all patient-facing actions for the mobile phone app and web browser portal:
/// - Online self-registration (phone + date of birth credentials)
/// - Managing family dependents (adding children, spouse, or elderly parents)
/// - Booking outpatient (OPD) doctor consultation slots
/// - Tracking live queue tokens and real-time waiting position
/// - Reviewing past completed visits and doctor diagnosis advice
/// </summary>
public interface IPatientService
{
    /// <summary>
    /// Registers a new patient account online using their phone number and date of birth.
    /// Returns a JWT session token for immediate login.
    /// </summary>
    Task<AuthResponse> SelfRegisterAsync(SelfRegisterRequest request);

    /// <summary>
    /// Adds a child, spouse, or elderly parent dependent under the primary user's phone number.
    /// Generates a permanent unique Medical Record Number (MRN) for the dependent.
    /// </summary>
    Task<PatientResponse> AddFamilyMemberAsync(Guid currentUserId, AddFamilyMemberRequest request);

    /// <summary>
    /// Retrieves all family member profiles registered under the user's phone number.
    /// Used by the booking wizard so the user can select who is seeing the doctor.
    /// </summary>
    Task<IEnumerable<PatientResponse>> GetMyFamilyMembersAsync(Guid currentUserId);

    /// <summary>
    /// Retrieves a specific patient profile by its unique database ID.
    /// </summary>
    Task<PatientResponse?> GetPatientByIdAsync(Guid patientId);

    /// <summary>
    /// Retrieves a patient profile linked to a specific user login account ID.
    /// </summary>
    Task<PatientResponse?> GetPatientByUserIdAsync(Guid userId);

    /// <summary>
    /// Updates patient demographic details (Name, Phone, DOB, Gender).
    /// Automatically cascades phone number updates to all family dependents.
    /// </summary>
    Task<PatientResponse> UpdatePatientAsync(Guid patientId, UpdatePatientRequest request);

    /// <summary>
    /// Retrieves past completed outpatient visits for an individual patient.
    /// </summary>
    Task<IEnumerable<PatientVisitHistoryDto>> GetPatientVisitHistoryAsync(Guid patientId);

    /// <summary>
    /// BATCH QUERY: Retrieves past visit history for multiple family members in a single SQL query.
    /// 
    /// WHY IS THIS CRITICAL FOR SCALABILITY?
    /// Instead of running multiple parallel database queries (which crashes EF Core's non-thread-safe DbContext),
    /// this method runs a single SQL "WHERE PatientId IN (...)" query using the indexed date column.
    /// </summary>
    Task<IEnumerable<PatientVisitHistoryDto>> GetFamilyVisitHistoryAsync(IEnumerable<Guid> patientIds);

    /// <summary>
    /// Books a consultation slot with a doctor today and issues an atomic, concurrency-safe token slip.
    /// </summary>
    Task<QueueTokenResponse> BookAppointmentAsync(BookTokenRequest request, Guid currentUserId);

    /// <summary>
    /// Retrieves all active consultation slips for today across the user and their family members.
    /// Calculates live queue position ("patients ahead") and estimated wait minutes.
    /// </summary>
    Task<IEnumerable<QueueTokenResponse>> GetMyActiveTokensAsync(Guid currentUserId);

    /// <summary>
    /// Retrieves today's active token for a specific patient ID.
    /// </summary>
    Task<QueueTokenResponse?> GetPatientActiveTokenAsync(Guid patientId);

    /// <summary>
    /// Cancels an active consultation token and immediately releases the queue spot to waiting patients.
    /// Records the cancellation reason directly on the token's consultation notes.
    /// </summary>
    Task<QueueTokenResponse> CancelMyTokenAsync(Guid tokenId, Guid currentUserId, string? reason = null);
}
