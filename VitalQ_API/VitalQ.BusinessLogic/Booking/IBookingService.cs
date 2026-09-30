using VitalQ.Entities.DTOs;

namespace VitalQ.BusinessLogic.Interfaces;

public interface IBookingService
{
    // 1. Get active clinical departments for the booking dropdown
    Task<IEnumerable<DepartmentResponse>> GetActiveDepartmentsAsync();

    // 2. Get available on-duty doctors in a department
    Task<IEnumerable<DoctorResponse>> GetAvailableDoctorsAsync(Guid departmentId);

    // 3. Book an appointment slot
    Task<QueueTokenResponse> BookTokenAsync(BookTokenRequest request);

    // 4. Current patient's active token today
    Task<QueueTokenResponse?> GetPatientActiveTokenAsync(Guid patientId);
}
