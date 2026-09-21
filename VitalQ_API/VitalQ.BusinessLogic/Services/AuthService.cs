using VitalQ.BusinessLogic.Interfaces;
using VitalQ.DataAccess.Repositories;
using VitalQ.Entities.DTOs;

namespace VitalQ.BusinessLogic.Services;

public class AuthService : IAuthService
{
    private readonly IPatientRepository _patientRepo;

    public AuthService(IPatientRepository patientRepo)
    {
        _patientRepo = patientRepo;
    }

    public Task<AuthResponse> RegisterPatientAsync(PatientRegisterRequest request)
    {
        throw new NotImplementedException();
    }

    public Task<AuthResponse?> LoginAsync(LoginRequest request)
    {
        throw new NotImplementedException();
    }

    public Task<AuthResponse?> RefreshTokenAsync(string refreshToken)
    {
        throw new NotImplementedException();
    }
}
