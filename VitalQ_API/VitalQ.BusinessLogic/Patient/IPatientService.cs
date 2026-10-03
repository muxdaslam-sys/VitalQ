using VitalQ.Entities.DTOs;

namespace VitalQ.BusinessLogic.Interfaces;

public interface IPatientService
{
    // 1. Patient self-registration (creates User + Patient, returns JWT session)
    Task<AuthResponse> SelfRegisterAsync(SelfRegisterRequest request);

    // 2. Logged-in patient adds a child or family dependent
    Task<PatientResponse> AddFamilyMemberAsync(Guid currentUserId, AddFamilyMemberRequest request);

    // 3. Logged-in patient views all profiles under their family phone number
    Task<IEnumerable<PatientResponse>> GetMyFamilyMembersAsync(Guid currentUserId);

    // 4. Nurse registers a walk-in patient at the clinic desk
    Task<PatientResponse> WalkInRegisterAsync(WalkInRegisterRequest request);

    // 5. Nurse searches patients by Phone, Name, or MRN
    Task<IEnumerable<PatientResponse>> SearchPatientsAsync(string query);

    // 6. Get patient profile by ID
    Task<PatientResponse?> GetPatientByIdAsync(Guid patientId);

    // 7. Update patient profile
    Task<PatientResponse> UpdatePatientAsync(Guid patientId, UpdatePatientRequest request);

    // 8. Get patient visit history
    Task<IEnumerable<PatientVisitHistoryDto>> GetPatientVisitHistoryAsync(Guid patientId);
}
