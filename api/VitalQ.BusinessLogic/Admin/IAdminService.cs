using VitalQ.Entities.DTOs;

namespace VitalQ.BusinessLogic.Interfaces;

/// <summary>
/// Defines administrative operations: staff accounts, doctors, departments, nursing stations, and patient directory.
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
