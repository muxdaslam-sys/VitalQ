using VitalQ.Entities.DTOs;

namespace VitalQ.BusinessLogic.Interfaces;

public interface IBookingService
{
    Task<QueueTokenResponse> BookTokenAsync(Guid patientId, BookTokenRequest request);
    Task<QueueTokenResponse> CreateWalkInTokenAsync(WalkInTokenRequest request);
    Task<QueueTokenResponse?> GetPatientActiveTokenAsync(Guid patientId);
    Task<IEnumerable<DepartmentResponse>> GetActiveDepartmentsAsync();
    Task<IEnumerable<DoctorResponse>> GetAvailableDoctorsAsync(Guid departmentId);
}
