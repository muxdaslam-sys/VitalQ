using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VitalQ.BusinessLogic.Interfaces;
using VitalQ.Entities.DTOs;

namespace VitalQ.API.Controllers;

/// <summary>
/// Dedicated Patient Portal Controller on Mobile and PC browser.
/// Handles online self-registration, family profiles, appointment booking, active tokens, and visit history.
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

    /// <summary>
    /// Online self-registration with Phone + Date of Birth.
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
        catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// List all family profiles registered under the user's phone number.
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
    /// Add a child, spouse, or parent dependent under the user's family account.
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
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// View past completed visits and doctor diagnosis notes for the current user and family.
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
            return NotFound(new { message = "No patient profiles found for this account." });

        var historyTasks = family.Select(member => _patientService.GetPatientVisitHistoryAsync(member.Id));
        var results = await Task.WhenAll(historyTasks);

        var combined = results
            .SelectMany(h => h)
            .OrderByDescending(h => h.BookedAtUtc)
            .ToList();

        return Ok(combined);
    }

    /// <summary>
    /// View past visit history for a specific patient ID.
    /// </summary>
    [Authorize(Roles = "Patient,Nurse,Doctor,Admin")]
    [HttpGet("history/{patientId:guid}")]
    public async Task<IActionResult> GetPatientVisitHistory(Guid patientId)
    {
        var history = await _patientService.GetPatientVisitHistoryAsync(patientId);
        return Ok(history);
    }

    /// <summary>
    /// Book an appointment slot online (generates atomic concurrency-safe token).
    /// </summary>
    [Authorize(Roles = "Patient,Nurse,Admin")]
    [HttpPost("bookings")]
    public async Task<IActionResult> BookAppointment([FromBody] BookTokenRequest request)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Guid.TryParse(userIdStr, out var currentUserId);

        // If patient ID not passed, default to caller's profile
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
    /// All active queue tokens today for the authenticated user and family dependents.
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
    /// Active queue token today for a specific patient ID.
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
    /// Cancel an active booking.
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
    /// Get patient profile by ID.
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
    /// Update patient profile.
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
}
