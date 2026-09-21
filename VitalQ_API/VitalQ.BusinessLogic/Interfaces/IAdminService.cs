using VitalQ.Entities.DTOs;

namespace VitalQ.BusinessLogic.Interfaces;

public interface IAdminService
{
    Task<DoctorResponse> CreateDoctorAsync(CreateDoctorRequest request);
    Task<IEnumerable<DoctorResponse>> GetAllDoctorsAsync();
    Task<DoctorResponse> UpdateDoctorStatusAsync(Guid doctorId, UpdateDoctorStatusRequest request);
    Task<UserResponse> CreateAdminUserAsync(CreateUserRequest request);

    // Department Management
    Task<DepartmentResponse> CreateDepartmentAsync(CreateDepartmentRequest request);
    Task<IEnumerable<DepartmentResponse>> GetAllDepartmentsAsync();
    Task<DepartmentResponse> UpdateDepartmentAsync(Guid id, UpdateDepartmentRequest request);
}
