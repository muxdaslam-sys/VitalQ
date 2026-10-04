using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VitalQ.BusinessLogic.Interfaces;
using VitalQ.DataAccess;
using VitalQ.Entities.DTOs;

namespace VitalQ.API.Controllers;

[ApiController]
[Route("api")]
public class BookingController : ControllerBase
{
    private readonly IPublicService _publicService;
    private readonly IPatientService _patientService;
    private readonly INurseService _nurseService;
    private readonly IQueueNotificationService _notificationService;
    private readonly VitalQDbContext _context;

    public BookingController(
        IPublicService publicService,
        IPatientService patientService,
        INurseService nurseService,
        IQueueNotificationService notificationService,
        VitalQDbContext context)
    {
        _publicService = publicService;
        _patientService = patientService;
        _nurseService = nurseService;
        _notificationService = notificationService;
        _context = context;
    }

    /// <summary>
    /// 1. Active clinical departments (Public)
    /// GET /api/departments
    /// </summary>
    [AllowAnonymous]
    [HttpGet("departments")]
    public async Task<IActionResult> GetDepartments()
    {
        var departments = await _publicService.GetActiveDepartmentsAsync();
        return Ok(departments);
    }

    /// <summary>
    /// 2. Available doctors in a department (Public)
    /// GET /api/departments/{id}/doctors
    /// </summary>
    [AllowAnonymous]
    [HttpGet("departments/{id}/doctors")]
    public async Task<IActionResult> GetDoctorsByDepartment(Guid id)
    {
        var doctors = await _publicService.GetAvailableDoctorsAsync(id);
        return Ok(doctors);
    }

    /// <summary>
    /// 3. Book an appointment slot -> generates atomic token (Patient, Nurse, Admin)
    /// POST /api/bookings
    /// </summary>
    [Authorize(Roles = "Patient,Nurse,Admin")]
    [HttpPost("bookings")]
    public async Task<IActionResult> BookToken([FromBody] BookTokenRequest request)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Guid.TryParse(userIdStr, out var currentUserId);

        // If patient ID was not provided, default to the authenticated user's own profile
        if (request.PatientId == Guid.Empty && currentUserId != Guid.Empty)
        {
            var selfPatient = await _context.Patients.FirstOrDefaultAsync(p => p.UserId == currentUserId);
            if (selfPatient != null)
            {
                request.PatientId = selfPatient.Id;
            }
        }

        if (request.PatientId == Guid.Empty)
        {
            return BadRequest(new { message = "PatientId is required." });
        }

        try
        {
            QueueTokenResponse token;

            // Route to appropriate service based on caller role
            if (User.IsInRole("Nurse") || User.IsInRole("Admin"))
            {
                token = await _nurseService.BookWalkInTokenAsync(request);
            }
            else
            {
                token = await _patientService.BookAppointmentAsync(request, currentUserId);
            }

            // Real-Time push to Doctor's PC room and Nurse's PC intake station
            await _notificationService.NotifyTokenBookedAsync(token);

            return Ok(token);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// 4. Current caller's active token today, if any (Patient, Admin)
    /// GET /api/bookings/my-token
    /// </summary>
    [Authorize(Roles = "Patient,Admin")]
    [HttpGet("bookings/my-token")]
    public async Task<IActionResult> GetMyToken([FromQuery] Guid? patientId)
    {
        var targetId = patientId ?? Guid.Empty;

        if (targetId == Guid.Empty)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(userIdStr, out var currentUserId))
            {
                var selfPatient = await _context.Patients.FirstOrDefaultAsync(p => p.UserId == currentUserId);
                if (selfPatient != null) targetId = selfPatient.Id;
            }
        }

        if (targetId == Guid.Empty)
        {
            return NotFound(new { message = "No patient profile found for current user." });
        }

        var token = await _patientService.GetPatientActiveTokenAsync(targetId);
        if (token == null)
        {
            return NotFound(new { message = "No active token found for today." });
        }
        return Ok(token);
    }

    /// <summary>
    /// 5. All active tokens today for authenticated user and family dependents (Patient, Admin)
    /// GET /api/bookings/my-tokens
    /// </summary>
    [Authorize(Roles = "Patient,Admin")]
    [HttpGet("bookings/my-tokens")]
    public async Task<IActionResult> GetMyTokens()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var currentUserId))
        {
            return Unauthorized();
        }

        var tokens = await _patientService.GetMyActiveTokensAsync(currentUserId);
        return Ok(tokens);
    }

    /// <summary>
    /// 6. Cancel an active booking (Patient, Admin)
    /// POST /api/bookings/{id:guid}/cancel
    /// </summary>
    [Authorize(Roles = "Patient,Admin")]
    [HttpPost("bookings/{id:guid}/cancel")]
    public async Task<IActionResult> CancelBooking([FromRoute] Guid id, [FromBody] CancelTokenRequest? request)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var currentUserId))
        {
            return Unauthorized();
        }

        try
        {
            var token = await _patientService.CancelMyTokenAsync(id, currentUserId, request?.Notes);

            // Real-Time push to Doctor console
            await _notificationService.NotifyQueueUpdatedAsync(token.DoctorId, token);

            return Ok(token);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// 7. Public live tracker for paper slip holders on PC or Mobile.
    /// GET /api/bookings/track/{tokenNumber}
    /// </summary>
    [AllowAnonymous]
    [HttpGet("bookings/track/{tokenNumber}")]
    public async Task<IActionResult> TrackToken(string tokenNumber)
    {
        if (string.IsNullOrWhiteSpace(tokenNumber))
        {
            return BadRequest(new { message = "Token number is required." });
        }

        var token = await _publicService.TrackTokenAsync(tokenNumber.Trim().ToUpper());
        if (token == null)
        {
            return NotFound(new { message = $"No active consultation slip found matching '{tokenNumber}' for today." });
        }

        return Ok(token);
    }
}
