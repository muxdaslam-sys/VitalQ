using VitalQ.Entities.DTOs;

namespace VitalQ.BusinessLogic.Interfaces;

/// <summary>
/// Public service for unauthenticated users, lobby kiosks, and paper slip tracking on PC or Mobile.
/// </summary>
public interface IPublicService
{
    // Active clinical departments (Cached for 2 min to protect DB)
    Task<IEnumerable<DepartmentResponse>> GetActiveDepartmentsAsync();

    // Available doctors on duty by department (Cached for 1 min)
    Task<IEnumerable<DoctorResponse>> GetAvailableDoctorsAsync(Guid departmentId);

    // Public live tracker for paper slip holders (Search by token number on Mobile or PC)
    Task<QueueTokenResponse?> TrackTokenAsync(string tokenNumber);
}
