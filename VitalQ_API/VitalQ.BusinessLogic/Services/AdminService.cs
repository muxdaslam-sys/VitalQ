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
}
