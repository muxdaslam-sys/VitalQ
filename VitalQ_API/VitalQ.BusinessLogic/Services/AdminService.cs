using Microsoft.EntityFrameworkCore;
using VitalQ.BusinessLogic.Interfaces;
using VitalQ.DataAccess;
using VitalQ.Entities.DTOs;
using VitalQ.Entities.Models;

namespace VitalQ.BusinessLogic.Services;

public class AdminService : IAdminService
{
    private readonly VitalQDbContext _context;

    public AdminService(VitalQDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Creates both a User account and a Doctor profile row.
    /// Stores the password in plain text as requested.
    /// </summary>
    public async Task<DoctorResponse> CreateDoctorAsync(CreateDoctorRequest request)
    {
        // 1. Verify that the requested Department exists
        var department = await _context.Departments.FindAsync(request.DepartmentId);
        if (department == null)
        {
            throw new ArgumentException($"Department with ID '{request.DepartmentId}' does not exist.");
        }

        // 2. Ensure Username is not already taken
        var usernameExists = await _context.Users.AnyAsync(u => u.Username == request.Username);
        if (usernameExists)
        {
            throw new InvalidOperationException($"Username '{request.Username}' is already in use.");
        }

        // 3. Ensure Phone Number is not already taken
        var phoneExists = await _context.Users.AnyAsync(u => u.PhoneNumber == request.PhoneNumber);
        if (phoneExists)
        {
            throw new InvalidOperationException($"Phone number '{request.PhoneNumber}' is already in use.");
        }

        // 4. Create the User account (Role = 'Doctor', plain text password)
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            Password = request.Password, // Plain password directly as requested
            FullName = request.FullName,
            PhoneNumber = request.PhoneNumber,
            Role = "Doctor",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        // 5. Create the Doctor profile linked to the User and Department
        var doctor = new Doctor
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            DepartmentId = department.Id,
            Specialization = request.Specialization,
            RoomNumber = request.RoomNumber,
            Status = "Available",
            AvgConsultationMinutes = request.AvgConsultationMinutes,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _context.Users.AddAsync(user);
        await _context.Doctors.AddAsync(doctor);
        await _context.SaveChangesAsync();

        return new DoctorResponse
        {
            Id = doctor.Id,
            UserId = user.Id,
            DoctorName = user.FullName,
            DepartmentId = department.Id,
            DepartmentName = department.Name,
            Specialization = doctor.Specialization,
            RoomNumber = doctor.RoomNumber,
            Status = doctor.Status,
            AvgConsultationMinutes = doctor.AvgConsultationMinutes,
            CreatedAtUtc = doctor.CreatedAtUtc
        };
    }

    public async Task<IEnumerable<DoctorResponse>> GetAllDoctorsAsync()
    {
        return await _context.Doctors
            .Include(d => d.User)
            .Include(d => d.Department)
            .Select(d => new DoctorResponse
            {
                Id = d.Id,
                UserId = d.UserId,
                DoctorName = d.User.FullName,
                DepartmentId = d.DepartmentId,
                DepartmentName = d.Department.Name,
                Specialization = d.Specialization,
                RoomNumber = d.RoomNumber,
                Status = d.Status,
                AvgConsultationMinutes = d.AvgConsultationMinutes,
                CreatedAtUtc = d.CreatedAtUtc
            })
            .ToListAsync();
    }

    public async Task<DoctorResponse> UpdateDoctorStatusAsync(Guid doctorId, UpdateDoctorStatusRequest request)
    {
        var doctor = await _context.Doctors
            .Include(d => d.User)
            .Include(d => d.Department)
            .FirstOrDefaultAsync(d => d.Id == doctorId);

        if (doctor == null)
        {
            throw new KeyNotFoundException($"Doctor with ID '{doctorId}' not found.");
        }

        doctor.Status = request.Status;
        await _context.SaveChangesAsync();

        return new DoctorResponse
        {
            Id = doctor.Id,
            UserId = doctor.UserId,
            DoctorName = doctor.User.FullName,
            DepartmentId = doctor.DepartmentId,
            DepartmentName = doctor.Department.Name,
            Specialization = doctor.Specialization,
            RoomNumber = doctor.RoomNumber,
            Status = doctor.Status,
            AvgConsultationMinutes = doctor.AvgConsultationMinutes,
            CreatedAtUtc = doctor.CreatedAtUtc
        };
    }

    public async Task<DoctorResponse> UpdateDoctorAsync(Guid doctorId, UpdateDoctorRequest request)
    {
        var doctor = await _context.Doctors
            .Include(d => d.User)
            .Include(d => d.Department)
            .FirstOrDefaultAsync(d => d.Id == doctorId);

        if (doctor == null)
        {
            throw new KeyNotFoundException($"Doctor with ID '{doctorId}' not found.");
        }

        // 1. Verify requested department exists
        var department = await _context.Departments.FindAsync(request.DepartmentId);
        if (department == null)
        {
            throw new ArgumentException($"Department with ID '{request.DepartmentId}' does not exist.");
        }

        // 2. Update User details if provided
        if (!string.IsNullOrWhiteSpace(request.FullName))
        {
            doctor.User.FullName = request.FullName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            doctor.User.PhoneNumber = request.PhoneNumber.Trim();
        }

        // 3. Update Doctor profile details
        doctor.DepartmentId = request.DepartmentId;
        doctor.Specialization = request.Specialization.Trim();
        doctor.RoomNumber = request.RoomNumber.Trim();
        doctor.Status = request.Status;
        doctor.AvgConsultationMinutes = request.AvgConsultationMinutes;

        await _context.SaveChangesAsync();

        return new DoctorResponse
        {
            Id = doctor.Id,
            UserId = doctor.UserId,
            DoctorName = doctor.User.FullName,
            DepartmentId = department.Id,
            DepartmentName = department.Name,
            Specialization = doctor.Specialization,
            RoomNumber = doctor.RoomNumber,
            Status = doctor.Status,
            AvgConsultationMinutes = doctor.AvgConsultationMinutes,
            CreatedAtUtc = doctor.CreatedAtUtc
        };
    }

    /// <summary>
    /// Creates an Admin user account with plain text password.
    /// </summary>
    public async Task<UserResponse> CreateAdminUserAsync(CreateUserRequest request)
    {
        var usernameExists = await _context.Users.AnyAsync(u => u.Username == request.Username);
        if (usernameExists)
        {
            throw new InvalidOperationException($"Username '{request.Username}' is already in use.");
        }

        var phoneExists = await _context.Users.AnyAsync(u => u.PhoneNumber == request.PhoneNumber);
        if (phoneExists)
        {
            throw new InvalidOperationException($"Phone number '{request.PhoneNumber}' is already in use.");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            Password = request.Password, // Plain password as requested
            FullName = request.FullName,
            PhoneNumber = request.PhoneNumber,
            Role = "Admin", // Strictly enforced on the backend
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        return new UserResponse
        {
            Id = user.Id,
            Username = user.Username,
            FullName = user.FullName,
            PhoneNumber = user.PhoneNumber,
            Role = user.Role,
            IsActive = user.IsActive,
            CreatedAtUtc = user.CreatedAtUtc
        };
    }

    // ==========================================
    // DEPARTMENT MANAGEMENT
    // ==========================================

    public async Task<DepartmentResponse> CreateDepartmentAsync(CreateDepartmentRequest request)
    {
        var codeUpper = request.Code.Trim().ToUpper();

        // 1. Ensure Department Code is unique
        var codeExists = await _context.Departments.AnyAsync(d => d.Code == codeUpper);
        if (codeExists)
        {
            throw new InvalidOperationException($"Department code '{codeUpper}' is already in use.");
        }

        // 2. Create the department (LastTokenNumber initialized to 0)
        var department = new Department
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Code = codeUpper,
            LocationFloor = request.LocationFloor.Trim(),
            LastTokenNumber = 0, // Starts at 0!
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _context.Departments.AddAsync(department);
        await _context.SaveChangesAsync();

        return new DepartmentResponse
        {
            Id = department.Id,
            Name = department.Name,
            Code = department.Code,
            LocationFloor = department.LocationFloor,
            LastTokenNumber = department.LastTokenNumber,
            IsActive = department.IsActive,
            CreatedAtUtc = department.CreatedAtUtc
        };
    }

    public async Task<IEnumerable<DepartmentResponse>> GetAllDepartmentsAsync()
    {
        return await _context.Departments
            .OrderBy(d => d.Name)
            .Select(d => new DepartmentResponse
            {
                Id = d.Id,
                Name = d.Name,
                Code = d.Code,
                LocationFloor = d.LocationFloor,
                LastTokenNumber = d.LastTokenNumber,
                IsActive = d.IsActive,
                CreatedAtUtc = d.CreatedAtUtc
            })
            .ToListAsync();
    }

    public async Task<DepartmentResponse> UpdateDepartmentAsync(Guid id, UpdateDepartmentRequest request)
    {
        var department = await _context.Departments.FindAsync(id);
        if (department == null)
        {
            throw new KeyNotFoundException($"Department with ID '{id}' not found.");
        }

        department.Name = request.Name.Trim();
        department.LocationFloor = request.LocationFloor.Trim();
        department.IsActive = request.IsActive;

        await _context.SaveChangesAsync();

        return new DepartmentResponse
        {
            Id = department.Id,
            Name = department.Name,
            Code = department.Code,
            LocationFloor = department.LocationFloor,
            LastTokenNumber = department.LastTokenNumber,
            IsActive = department.IsActive,
            CreatedAtUtc = department.CreatedAtUtc
        };
    }

    // ==========================================
    // NURSING STATION MANAGEMENT
    // ==========================================

    public async Task<NursingStationResponse> CreateNursingStationAsync(CreateNursingStationRequest request)
    {
        string? departmentName = null;

        // 1. If DepartmentId is supplied, verify it exists
        if (request.DepartmentId.HasValue)
        {
            var department = await _context.Departments.FindAsync(request.DepartmentId.Value);
            if (department == null)
            {
                throw new ArgumentException($"Department with ID '{request.DepartmentId}' does not exist.");
            }
            departmentName = department.Name;
        }

        // 2. Create the Nursing Station
        var station = new NursingStation
        {
            Id = Guid.NewGuid(),
            StationName = request.StationName.Trim(),
            DepartmentId = request.DepartmentId,
            LocationFloor = request.LocationFloor.Trim(),
            IsActive = true
        };

        await _context.NursingStations.AddAsync(station);
        await _context.SaveChangesAsync();

        return new NursingStationResponse
        {
            Id = station.Id,
            StationName = station.StationName,
            DepartmentId = station.DepartmentId,
            DepartmentName = departmentName,
            LocationFloor = station.LocationFloor,
            IsActive = station.IsActive
        };
    }

    public async Task<IEnumerable<NursingStationResponse>> GetAllNursingStationsAsync(Guid? departmentId = null)
    {
        var query = _context.NursingStations
            .Include(s => s.Department)
            .AsQueryable();

        if (departmentId.HasValue)
        {
            query = query.Where(s => s.DepartmentId == departmentId.Value);
        }

        return await query
            .OrderBy(s => s.LocationFloor)
            .ThenBy(s => s.StationName)
            .Select(s => new NursingStationResponse
            {
                Id = s.Id,
                StationName = s.StationName,
                DepartmentId = s.DepartmentId,
                DepartmentName = s.Department != null ? s.Department.Name : null,
                LocationFloor = s.LocationFloor,
                IsActive = s.IsActive
            })
            .ToListAsync();
    }

    public async Task<NursingStationResponse> UpdateNursingStationAsync(Guid id, UpdateNursingStationRequest request)
    {
        var station = await _context.NursingStations
            .Include(s => s.Department)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (station == null)
        {
            throw new KeyNotFoundException($"Nursing station with ID '{id}' not found.");
        }

        string? departmentName = null;
        if (request.DepartmentId.HasValue)
        {
            var department = await _context.Departments.FindAsync(request.DepartmentId.Value);
            if (department == null)
            {
                throw new ArgumentException($"Department with ID '{request.DepartmentId}' does not exist.");
            }
            departmentName = department.Name;
        }

        station.StationName = request.StationName.Trim();
        station.DepartmentId = request.DepartmentId;
        station.LocationFloor = request.LocationFloor.Trim();
        station.IsActive = request.IsActive;

        await _context.SaveChangesAsync();

        return new NursingStationResponse
        {
            Id = station.Id,
            StationName = station.StationName,
            DepartmentId = station.DepartmentId,
            DepartmentName = departmentName ?? station.Department?.Name,
            LocationFloor = station.LocationFloor,
            IsActive = station.IsActive
        };
    }

    // ==========================================
    // PATIENT DIRECTORY & VISIT HISTORY
    // ==========================================

    public async Task<IEnumerable<PatientDetailResponse>> GetPatientDirectoryAsync()
    {
        var patients = await _context.Patients
            .Include(p => p.QueueTokens)
                .ThenInclude(t => t.Department)
            .Include(p => p.QueueTokens)
                .ThenInclude(t => t.Doctor)
                    .ThenInclude(d => d.User)
            .Include(p => p.QueueTokens)
                .ThenInclude(t => t.TriageAssessment)
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToListAsync();

        return patients.Select(p => new PatientDetailResponse
        {
            Id = p.Id,
            MedicalRecordNumber = p.MedicalRecordNumber,
            FullName = p.FullName,
            PhoneNumber = p.PhoneNumber,
            DateOfBirth = p.DateOfBirth,
            Gender = p.Gender,
            IsRegisteredAppUser = p.UserId.HasValue,
            CreatedAtUtc = p.CreatedAtUtc,
            Visits = p.QueueTokens
                .OrderByDescending(t => t.BookedAtUtc)
                .Select(t => new PatientVisitHistoryDto
                {
                    TokenId = t.Id,
                    TokenNumber = t.TokenNumber,
                    DepartmentName = t.Department.Name,
                    DoctorName = t.Doctor.User.FullName,
                    Status = t.Status,
                    TriageLevel = t.TriageAssessment != null ? t.TriageAssessment.TriageLevel : null,
                    BookedAtUtc = t.BookedAtUtc,
                    TriagedAtUtc = t.TriagedAtUtc,
                    CalledAtUtc = t.CalledAtUtc,
                    CompletedAtUtc = t.CompletedAtUtc,
                    ConsultationNotes = t.ConsultationNotes
                })
                .ToList()
        });
    }

    public async Task<PatientDetailResponse?> GetPatientDetailsByIdAsync(Guid patientId)
    {
        var patient = await _context.Patients
            .Include(p => p.QueueTokens)
                .ThenInclude(t => t.Department)
            .Include(p => p.QueueTokens)
                .ThenInclude(t => t.Doctor)
                    .ThenInclude(d => d.User)
            .Include(p => p.QueueTokens)
                .ThenInclude(t => t.TriageAssessment)
            .FirstOrDefaultAsync(p => p.Id == patientId);

        if (patient == null) return null;

        return new PatientDetailResponse
        {
            Id = patient.Id,
            MedicalRecordNumber = patient.MedicalRecordNumber,
            FullName = patient.FullName,
            PhoneNumber = patient.PhoneNumber,
            DateOfBirth = patient.DateOfBirth,
            Gender = patient.Gender,
            IsRegisteredAppUser = patient.UserId.HasValue,
            CreatedAtUtc = patient.CreatedAtUtc,
            Visits = patient.QueueTokens
                .OrderByDescending(t => t.BookedAtUtc)
                .Select(t => new PatientVisitHistoryDto
                {
                    TokenId = t.Id,
                    TokenNumber = t.TokenNumber,
                    DepartmentName = t.Department.Name,
                    DoctorName = t.Doctor.User.FullName,
                    Status = t.Status,
                    TriageLevel = t.TriageAssessment != null ? t.TriageAssessment.TriageLevel : null,
                    BookedAtUtc = t.BookedAtUtc,
                    TriagedAtUtc = t.TriagedAtUtc,
                    CalledAtUtc = t.CalledAtUtc,
                    CompletedAtUtc = t.CompletedAtUtc,
                    ConsultationNotes = t.ConsultationNotes
                })
                .ToList()
        };
    }
}
