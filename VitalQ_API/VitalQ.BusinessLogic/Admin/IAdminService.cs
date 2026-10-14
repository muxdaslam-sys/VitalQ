using VitalQ.Entities.DTOs;

namespace VitalQ.BusinessLogic.Interfaces;

/// <summary>
/// CONTRACT: Hospital Administration & Staff Governance Service
/// 
/// PURPOSE:
/// Defines core administrative controls across hospital operational units:
/// - Section 1: Staff & Admin Account Management
/// - Section 2: Doctor Roster & Practice Profile
/// - Section 3: Clinical Department Management
/// - Section 4: Nursing Station Management
/// - Section 5: Patient Directory & Clinical Lookup
/// </summary>
public interface IAdminService
{
    #region 1. Staff & Admin Account Management
    Task<StaffUserResponse> CreateAdminUserAsync(CreateUserRequest request);
    Task<IEnumerable<StaffUserResponse>> GetAllStaffUsersAsync();
    Task<StaffUserResponse> UpdateUserAsync(Guid id, UpdateUserRequest request);
    #endregion

    #region 2. Doctor Roster & Practice Profile
    Task<DoctorResponse> CreateDoctorAsync(CreateDoctorRequest request);
    Task<IEnumerable<DoctorResponse>> GetAllDoctorsAsync();
    Task<DoctorResponse> UpdateDoctorAsync(Guid doctorId, UpdateDoctorRequest request);
    Task<DoctorResponse> UpdateDoctorStatusAsync(Guid doctorId, UpdateDoctorStatusRequest request);
    #endregion

    #region 3. Clinical Department Management
    Task<DepartmentResponse> CreateDepartmentAsync(CreateDepartmentRequest request);
    Task<IEnumerable<DepartmentResponse>> GetAllDepartmentsAsync();
    Task<DepartmentResponse> UpdateDepartmentAsync(Guid id, UpdateDepartmentRequest request);
    #endregion

    #region 4. Nursing Station Management
    Task<NursingStationResponse> CreateNursingStationAsync(CreateNursingStationRequest request);
    Task<IEnumerable<NursingStationResponse>> GetAllNursingStationsAsync(Guid? departmentId = null);
    Task<NursingStationResponse> UpdateNursingStationAsync(Guid id, UpdateNursingStationRequest request);
    #endregion

    #region 5. Patient Directory & Clinical Lookup
    Task<IEnumerable<PatientDetailResponse>> GetPatientDirectoryAsync(string? search = null);
    Task<PatientDetailResponse?> GetPatientDetailsByIdAsync(Guid patientId);
    #endregion
}
