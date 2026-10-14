using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VitalQ.BusinessLogic.Interfaces;
using VitalQ.Entities.DTOs;

namespace VitalQ.API.Controllers;

/// <summary>
/// CONTROLLER: Dedicated Patient Outpatient Portal API
/// Route: /api/patient
/// 
/// PURPOSE:
/// Serves all HTTP requests from the patient's mobile phone browser and PC desktop portal:
/// - Section 1: Demographics & Family Profiles (Self-registration, family members, profile management)
/// - Section 2: Appointment Booking & Live Queue Tracking (Slots booking, active tokens, cancellation)
/// - Section 3: Consultation & Clinical History (Batch and individual encounter records)
/// </summary>
[ApiController]
[Route("api/patient")]
public class PatientController : ControllerBase
{
    private readonly IPatientService _patientService;
    private readonly IQueueNotificationService _notificationService;

    public PatientController(IPatientService patientService, IQueueNotificationService notificationService)
    {
        _patientService = patientService;
        _notificationService = notificationService;
    }

    #region 1. Demographics & Family Profiles

    /// <summary>
    /// ENDPOINT: POST /api/patient/self-register
    /// Online patient self-registration using Phone Number and Date of Birth.
    /// Sets an HttpOnly refresh token cookie and returns JWT access token for immediate login.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("self-register")]
    public async Task<IActionResult> SelfRegister([FromBody] SelfRegisterRequest request)
    {
        try
        {
            var result = await _patientService.SelfRegisterAsync(request);
            if (!string.IsNullOrEmpty(result.RefreshToken))
            {
                // Set secure HttpOnly cookie for silent background token rotation
                Response.Cookies.Append("refreshToken", result.RefreshToken, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict,
                    Expires = DateTime.UtcNow.AddDays(7)
                });
            }
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "An account with this phone number already exists." });
        }
    }

    /// <summary>
    /// ENDPOINT: GET /api/patient/family
    /// Returns all patient profiles registered under the user's family mobile number.
    /// </summary>
    [Authorize(Roles = "Patient")]
    [HttpGet("family")]
    public async Task<IActionResult> GetMyFamily()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var currentUserId))
        {
            return Unauthorized();
        }

        var results = await _patientService.GetMyFamilyMembersAsync(currentUserId);
        return Ok(results);
    }

    /// <summary>
    /// ENDPOINT: POST /api/patient/family
    /// Adds a child, spouse, or elderly parent dependent under the logged-in user's account.
    /// </summary>
    [Authorize(Roles = "Patient")]
    [HttpPost("family")]
    public async Task<IActionResult> AddFamilyMember([FromBody] AddFamilyMemberRequest request)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var currentUserId))
        {
            return Unauthorized();
        }

        try
        {
            var result = await _patientService.AddFamilyMemberAsync(currentUserId, request);
            return Ok(result);
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
    /// ENDPOINT: GET /api/patient/profile/{id}
    /// Retrieves a patient demographic profile by ID.
    /// </summary>
    [Authorize(Roles = "Patient,Nurse,Admin,Doctor")]
    [HttpGet("profile/{id:guid}")]
    public async Task<IActionResult> GetProfile(Guid id)
    {
        var patient = await _patientService.GetPatientByIdAsync(id);
        if (patient == null)
        {
            return NotFound(new { message = $"Patient with ID {id} was not found." });
        }
        return Ok(patient);
    }

    /// <summary>
    /// ENDPOINT: PUT /api/patient/profile/{id}
    /// Updates patient demographic details.
    /// </summary>
    [Authorize(Roles = "Patient,Nurse,Admin")]
    [HttpPut("profile/{id:guid}")]
    public async Task<IActionResult> UpdateProfile(Guid id, [FromBody] UpdatePatientRequest request)
    {
        try
        {
            var result = await _patientService.UpdatePatientAsync(id, request);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    #endregion

    #region 2. Appointment Booking & Live Queue Tracking

    /// <summary>
    /// ENDPOINT: POST /api/patient/bookings
    /// Books an outpatient consultation slot for today and generates an atomic token.
    /// Broadcasts SignalR "TokenBooked" event to the Doctor's room and Nurse desk.
    /// </summary>
    [Authorize(Roles = "Patient,Nurse,Admin")]
    [HttpPost("bookings")]
    public async Task<IActionResult> BookAppointment([FromBody] BookTokenRequest request)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Guid.TryParse(userIdStr, out var currentUserId);

        // If patient ID not passed, default to caller's self profile
        if (request.PatientId == Guid.Empty && currentUserId != Guid.Empty)
        {
            var selfPatient = await _patientService.GetPatientByUserIdAsync(currentUserId);
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
            var token = await _patientService.BookAppointmentAsync(request, currentUserId);
            
            // Push real-time notification to doctor PC and nurse triage desk
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
    /// ENDPOINT: GET /api/patient/bookings/active
    /// Returns all active queue tokens today for the authenticated user and family dependents.
    /// Shows live queue position ("patients ahead") and calculated estimated wait time.
    /// </summary>
    [Authorize(Roles = "Patient,Admin")]
    [HttpGet("bookings/active")]
    public async Task<IActionResult> GetActiveBookings()
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
    /// ENDPOINT: GET /api/patient/bookings/active/{patientId}
    /// Returns active consultation token today for a specific patient ID.
    /// </summary>
    [Authorize(Roles = "Patient,Admin")]
    [HttpGet("bookings/active/{patientId:guid}")]
    public async Task<IActionResult> GetPatientActiveBooking(Guid patientId)
    {
        if (patientId == Guid.Empty)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(userIdStr, out var currentUserId))
            {
                var selfPatient = await _patientService.GetPatientByUserIdAsync(currentUserId);
                if (selfPatient != null) patientId = selfPatient.Id;
            }
        }

        if (patientId == Guid.Empty)
        {
            return NotFound(new { message = "No patient profile found for current user." });
        }

        var token = await _patientService.GetPatientActiveTokenAsync(patientId);
        if (token == null)
        {
            return NotFound(new { message = "No active token found for today." });
        }
        return Ok(token);
    }

    /// <summary>
    /// ENDPOINT: POST /api/patient/bookings/{id}/cancel
    /// Cancels an active appointment booking and notifies waiting room clients via SignalR.
    /// </summary>
    [Authorize(Roles = "Patient,Admin")]
    [HttpPost("bookings/{id:guid}/cancel")]
    public async Task<IActionResult> CancelBooking(Guid id, [FromBody] CancelTokenRequest? request)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var currentUserId))
        {
            return Unauthorized();
        }

        try
        {
            var token = await _patientService.CancelMyTokenAsync(id, currentUserId, request?.Notes);
            
            // Notify doctor PC console so the live queue counter refreshes immediately
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

    #endregion

    #region 3. Consultation & Clinical History

    /// <summary>
    /// ENDPOINT: GET /api/patient/history
    /// Retrieves past completed outpatient encounters and doctor advice for the user and their family.
    /// </summary>
    [Authorize(Roles = "Patient")]
    [HttpGet("history")]
    public async Task<IActionResult> GetMyVisitHistory()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var currentUserId))
            return Unauthorized();

        var family = await _patientService.GetMyFamilyMembersAsync(currentUserId);
        if (!family.Any())
            return Ok(Enumerable.Empty<PatientVisitHistoryDto>());

        var familyIds = family.Select(m => m.Id);
        var combined = await _patientService.GetFamilyVisitHistoryAsync(familyIds);

        return Ok(combined);
    }

    /// <summary>
    /// ENDPOINT: GET /api/patient/history/{patientId}
    /// Retrieves past visit history for a specific individual patient ID.
    /// </summary>
    [Authorize(Roles = "Patient,Nurse,Doctor,Admin")]
    [HttpGet("history/{patientId:guid}")]
    public async Task<IActionResult> GetPatientVisitHistory(Guid patientId)
    {
        var history = await _patientService.GetPatientVisitHistoryAsync(patientId);
        return Ok(history);
    }

    #endregion
}
