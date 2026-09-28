using VitalQ.BusinessLogic.Interfaces;
using VitalQ.DataAccess;
using VitalQ.Entities.DTOs;

namespace VitalQ.BusinessLogic.Services;

public class BookingService : IBookingService
{
    private readonly VitalQDbContext _context;

    public BookingService(VitalQDbContext context)
    {
        _context = context;
    }

    public Task<QueueTokenResponse> BookTokenAsync(Guid patientId, BookTokenRequest request)
    {
        throw new NotImplementedException();
    }

    public Task<QueueTokenResponse> CreateWalkInTokenAsync(WalkInTokenRequest request)
    {
        throw new NotImplementedException();
    }

    public Task<QueueTokenResponse?> GetPatientActiveTokenAsync(Guid patientId)
    {
        throw new NotImplementedException();
    }

    public Task<IEnumerable<DepartmentResponse>> GetActiveDepartmentsAsync()
    {
        throw new NotImplementedException();
    }

    public Task<IEnumerable<DoctorResponse>> GetAvailableDoctorsAsync(Guid departmentId)
    {
        throw new NotImplementedException();
    }
}
