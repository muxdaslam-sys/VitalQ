using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VitalQ.BusinessLogic.Interfaces;
using VitalQ.Entities.DTOs;

namespace VitalQ.API.Controllers;

[Authorize(Roles = "Admin")]
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
    /// Update doctor details (PUT /api/admin/doctors/{id})
    /// </summary>
    [HttpPut("doctors/{id}")]
    public async Task<IActionResult> UpdateDoctor(Guid id, [FromBody] UpdateDoctorRequest request)
    {
        try
        {
            var doctor = await _adminService.UpdateDoctorAsync(id, request);
            return Ok(doctor);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while updating the doctor.", details = ex.Message });
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
    // NURSING STATION MANAGEMENT
    // ==========================================

    /// <summary>
    /// List all nursing stations, optionally filtered by department (GET /api/admin/nursing-stations?departmentId=...)
    /// </summary>
    [HttpGet("nursing-stations")]
    public async Task<IActionResult> GetNursingStations([FromQuery] Guid? departmentId = null)
    {
        var stations = await _adminService.GetAllNursingStationsAsync(departmentId);
        return Ok(stations);
    }

    /// <summary>
    /// Create a new nursing desk (POST /api/admin/nursing-stations)
    /// </summary>
    [HttpPost("nursing-stations")]
    public async Task<IActionResult> CreateNursingStation([FromBody] CreateNursingStationRequest request)
    {
        try
        {
            var station = await _adminService.CreateNursingStationAsync(request);
            return CreatedAtAction(nameof(GetNursingStations), new { id = station.Id }, station);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while creating the nursing station.", details = ex.Message });
        }
    }

    /// <summary>
    /// Update nursing desk details (PUT /api/admin/nursing-stations/{id})
    /// </summary>
    [HttpPut("nursing-stations/{id}")]
    public async Task<IActionResult> UpdateNursingStation(Guid id, [FromBody] UpdateNursingStationRequest request)
    {
        try
        {
            var station = await _adminService.UpdateNursingStationAsync(id, request);
            return Ok(station);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while updating the nursing station.", details = ex.Message });
        }
    }

    // ==========================================
    // PATIENT DIRECTORY
    // ==========================================

    /// <summary>
    /// Browse all registered and walk-in patients with their visit histories (GET /api/admin/patients)
    /// </summary>
    [HttpGet("patients")]
    public async Task<IActionResult> GetPatients()
    {
        var patients = await _adminService.GetPatientDirectoryAsync();
        return Ok(patients);
    }

    /// <summary>
    /// Get a single patient's full profile and visit history (GET /api/admin/patients/{id})
    /// </summary>
    [HttpGet("patients/{id}")]
    public async Task<IActionResult> GetPatientById(Guid id)
    {
        var patient = await _adminService.GetPatientDetailsByIdAsync(id);
        if (patient == null)
        {
            return NotFound(new { message = $"Patient with ID '{id}' not found." });
        }
        return Ok(patient);
    }
}
