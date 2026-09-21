using Microsoft.AspNetCore.Mvc;
using VitalQ.BusinessLogic.Interfaces;
using VitalQ.Entities.DTOs;

namespace VitalQ.API.Controllers;

[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;

    public AdminController(IAdminService adminService)
    {
        _adminService = adminService;
    }

    // ==========================================
    // ADMIN / STAFF USER CREATION
    // ==========================================

    /// <summary>
    /// Create an Admin or Staff user account (POST /api/admin/users).
    /// </summary>
    [HttpPost("users")]
    public async Task<IActionResult> CreateAdminUser([FromBody] CreateUserRequest request)
    {
        try
        {
            var user = await _adminService.CreateAdminUserAsync(request);
            return Ok(user);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while creating the user.", details = ex.Message });
        }
    }

    // ==========================================
    // DOCTOR MANAGEMENT
    // ==========================================

    /// <summary>
    /// List all doctors with their user and department details.
    /// </summary>
    [HttpGet("doctors")]
    public async Task<IActionResult> GetDoctors()
    {
        var doctors = await _adminService.GetAllDoctorsAsync();
        return Ok(doctors);
    }

    /// <summary>
    /// Add a new doctor with login credentials (POST /api/admin/doctors).
    /// </summary>
    [HttpPost("doctors")]
    public async Task<IActionResult> CreateDoctor([FromBody] CreateDoctorRequest request)
    {
        try
        {
            var doctor = await _adminService.CreateDoctorAsync(request);
            return CreatedAtAction(nameof(GetDoctors), new { id = doctor.Id }, doctor);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while creating the doctor.", details = ex.Message });
        }
    }

    /// <summary>
    /// Quick toggle between Available and OnLeave (§07 Admin portal).
    /// </summary>
    [HttpPatch("doctors/{id}/status")]
    public async Task<IActionResult> ToggleDoctorStatus(Guid id, [FromBody] UpdateDoctorStatusRequest request)
    {
        try
        {
            var doctor = await _adminService.UpdateDoctorStatusAsync(id, request);
            return Ok(doctor);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    // ==========================================
    // DEPARTMENT MANAGEMENT
    // ==========================================

    /// <summary>
    /// List all departments (GET /api/admin/departments)
    /// </summary>
    [HttpGet("departments")]
    public async Task<IActionResult> GetDepartments()
    {
        var departments = await _adminService.GetAllDepartmentsAsync();
        return Ok(departments);
    }

    /// <summary>
    /// Create a new department with unique code prefix (POST /api/admin/departments)
    /// </summary>
    [HttpPost("departments")]
    public async Task<IActionResult> CreateDepartment([FromBody] CreateDepartmentRequest request)
    {
        try
        {
            var department = await _adminService.CreateDepartmentAsync(request);
            return CreatedAtAction(nameof(GetDepartments), new { id = department.Id }, department);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while creating the department.", details = ex.Message });
        }
    }

    /// <summary>
    /// Update department details (PUT /api/admin/departments/{id})
    /// </summary>
    [HttpPut("departments/{id}")]
    public async Task<IActionResult> UpdateDepartment(Guid id, [FromBody] UpdateDepartmentRequest request)
    {
        try
        {
            var department = await _adminService.UpdateDepartmentAsync(id, request);
            return Ok(department);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while updating the department.", details = ex.Message });
        }
    }

    // ==========================================
    // NURSING STATION MANAGEMENT (Placeholders)
    // ==========================================
    [HttpGet("nursing-stations")]
    public Task<IActionResult> GetNursingStations() => Task.FromResult<IActionResult>(Ok());

    [HttpPost("nursing-stations")]
    public Task<IActionResult> CreateNursingStation([FromBody] CreateNursingStationRequest request) => Task.FromResult<IActionResult>(Ok());

    [HttpPut("nursing-stations/{id}")]
    public Task<IActionResult> UpdateNursingStation(Guid id, [FromBody] UpdateNursingStationRequest request) => Task.FromResult<IActionResult>(Ok());

    // ==========================================
    // PATIENT DIRECTORY & SEEDER
    // ==========================================
    [HttpGet("patients")]
    public Task<IActionResult> GetPatients() => Task.FromResult<IActionResult>(Ok());

    [HttpPost("seed/demo-data")]
    public Task<IActionResult> SeedDemoData() => Task.FromResult<IActionResult>(Ok(new { message = "Demo data seeded successfully" }));
}
