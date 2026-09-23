using VitalQ.Entities.DTOs;

namespace VitalQ.BusinessLogic.Interfaces;

public interface IAdminService
{
    Task<DoctorResponse> CreateDoctorAsync(CreateDoctorRequest request);
    Task<IEnumerable<DoctorResponse>> GetAllDoctorsAsync();
    Task<DoctorResponse> UpdateDoctorAsync(Guid doctorId, UpdateDoctorRequest request);
    Task<DoctorResponse> UpdateDoctorStatusAsync(Guid doctorId, UpdateDoctorStatusRequest request);
    Task<UserResponse> CreateAdminUserAsync(CreateUserRequest request);

    // Department Management
    Task<DepartmentResponse> CreateDepartmentAsync(CreateDepartmentRequest request);
    Task<IEnumerable<DepartmentResponse>> GetAllDepartmentsAsync();
    Task<DepartmentResponse> UpdateDepartmentAsync(Guid id, UpdateDepartmentRequest request);

    // Nursing Station Management
    Task<NursingStationResponse> CreateNursingStationAsync(CreateNursingStationRequest request);
    Task<IEnumerable<NursingStationResponse>> GetAllNursingStationsAsync(Guid? departmentId = null);
    Task<NursingStationResponse> UpdateNursingStationAsync(Guid id, UpdateNursingStationRequest request);

    // Patient Directory & Visit History
    Task<IEnumerable<PatientDetailResponse>> GetPatientDirectoryAsync();
    Task<PatientDetailResponse?> GetPatientDetailsByIdAsync(Guid patientId);
}
