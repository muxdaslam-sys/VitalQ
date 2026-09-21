using VitalQ.BusinessLogic.Interfaces;
using VitalQ.DataAccess.Repositories;
using VitalQ.Entities.DTOs;

namespace VitalQ.BusinessLogic.Services;

public class BookingService : IBookingService
{
    private readonly IQueueTokenRepository _tokenRepo;
    private readonly IDepartmentRepository _deptRepo;
    private readonly IDoctorRepository _doctorRepo;
    private readonly IPatientRepository _patientRepo;

    public BookingService(
        IQueueTokenRepository tokenRepo,
        IDepartmentRepository deptRepo,
        IDoctorRepository doctorRepo,
        IPatientRepository patientRepo)
    {
        _tokenRepo = tokenRepo;
        _deptRepo = deptRepo;
        _doctorRepo = doctorRepo;
        _patientRepo = patientRepo;
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
