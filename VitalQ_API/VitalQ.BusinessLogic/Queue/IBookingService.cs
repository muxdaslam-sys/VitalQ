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

    // 5. All active tokens today for authenticated user and family dependents
    Task<IEnumerable<QueueTokenResponse>> GetUserActiveTokensAsync(Guid currentUserId);

    // 6. Cancel an active booking (Patient or Admin)
    Task<QueueTokenResponse> CancelTokenAsync(Guid tokenId, Guid currentUserId, string? reason = null);

    // 7. Public live tracker for paper slip holders (no authentication required)
    Task<QueueTokenResponse?> TrackTokenAsync(string tokenNumber);
}
