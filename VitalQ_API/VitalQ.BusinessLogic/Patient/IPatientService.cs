using VitalQ.Entities.DTOs;

namespace VitalQ.BusinessLogic.Interfaces;

/// <summary>
/// Dedicated service for the Patient Portal on Mobile and PC browser.
/// Handles online self-registration, family profiles, appointment booking, active tokens, and visit history.
/// </summary>
public interface IPatientService
{
    // 1. Patient self-registration online (Phone + DOB password -> JWT session)
    Task<AuthResponse> SelfRegisterAsync(SelfRegisterRequest request);

    // 2. Add family dependent (Child, Spouse, Parent under user's family phone)
    Task<PatientResponse> AddFamilyMemberAsync(Guid currentUserId, AddFamilyMemberRequest request);

    // 3. View all patient profiles belonging to user's family
    Task<IEnumerable<PatientResponse>> GetMyFamilyMembersAsync(Guid currentUserId);

    // 4. Patient profile by ID
    Task<PatientResponse?> GetPatientByIdAsync(Guid patientId);

    // 5. Update patient profile
    Task<PatientResponse> UpdatePatientAsync(Guid patientId, UpdatePatientRequest request);

    // 6. Aggregated visit history for a patient
    Task<IEnumerable<PatientVisitHistoryDto>> GetPatientVisitHistoryAsync(Guid patientId);

    // 7. Book appointment slot online (for self or family dependent)
    Task<QueueTokenResponse> BookAppointmentAsync(BookTokenRequest request, Guid currentUserId);

    // 8. Active queue tokens today for user and family members (Shows live position ahead & ETA)
    Task<IEnumerable<QueueTokenResponse>> GetMyActiveTokensAsync(Guid currentUserId);

    // 9. Get active token today for a specific patient ID
    Task<QueueTokenResponse?> GetPatientActiveTokenAsync(Guid patientId);

    // 10. Cancel an active appointment booking
    Task<QueueTokenResponse> CancelMyTokenAsync(Guid tokenId, Guid currentUserId, string? reason = null);
}
