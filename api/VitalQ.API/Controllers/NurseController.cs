using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VitalQ.BusinessLogic.Interfaces;
using VitalQ.Entities.DTOs;

namespace VitalQ.API.Controllers;

/// <summary>
/// Exposes nurse station endpoints: patient lookup, walk-in registration, token issuance, and clinical triage.
/// </summary>
[Authorize(Roles = "Nurse,Admin")]
[ApiController]
[Route("api/nurse")]
public class NurseController : ControllerBase
{
    private readonly INurseService _nurseService;
    private readonly IQueueNotificationService _notificationService;

    public NurseController(INurseService nurseService, IQueueNotificationService notificationService)
    {
        _nurseService = nurseService;
        _notificationService = notificationService;
    }

    #region 1. Patient Lookup & Identification

    /// <summary>
    /// Search patients by Token Number, MRN, Phone, or Name for intake at the clinic desk (GET /api/nurse/patients/search?query=...).
    /// </summary>
    [HttpGet("patients/search")]
    public async Task<IActionResult> SearchPatients([FromQuery] string query)
    {
        var result = await _nurseService.SearchPatientsAsync(query);
        return Ok(result);
    }

    #endregion

    #region 2. Walk-In Registration & Identity Linking

    /// <summary>
    /// Register a walk-in patient in-person at the clinic reception desk (POST /api/nurse/patients/walk-in).
    /// </summary>
    [HttpPost("patients/walk-in")]
    public async Task<IActionResult> RegisterWalkIn([FromBody] WalkInRegisterRequest request)
    {
        try
        {
            var result = await _nurseService.RegisterWalkInPatientAsync(request);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (Exception)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An internal error occurred while registering the walk-in patient." });
        }
    }

    #endregion

    #region 3. Reception Desk Token Issuance

    /// <summary>
    /// Issue a walk-in queue slip directly at the counter (POST /api/nurse/tokens/book).
    /// </summary>
    [HttpPost("tokens/book")]
    public async Task<IActionResult> BookWalkInToken([FromBody] BookTokenRequest request)
    {
        try
        {
            var token = await _nurseService.BookWalkInTokenAsync(request);
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
        catch (Exception)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An internal error occurred while issuing the walk-in token." });
        }
    }

    #endregion

    #region 4. Clinical Triage & Vital Signs Intake

    /// <summary>
    /// List patients booked today awaiting vital signs intake (GET /api/nurse/triage/pending?departmentId=...&nursingStationId=...).
    /// </summary>
    [HttpGet("triage/pending")]
    public async Task<IActionResult> GetPendingTriage([FromQuery] Guid? departmentId = null, [FromQuery] Guid? nursingStationId = null)
    {
        var result = await _nurseService.GetPendingTriageQueueAsync(departmentId, nursingStationId);
        return Ok(result);
    }

    /// <summary>
    /// Record vitals (BP, SpO2, HR, Temp, Pain) and assign CTAS urgency level (POST /api/nurse/triage/{id}).
    /// </summary>
    [HttpPost("triage/{id:guid}")]
    public async Task<IActionResult> SubmitTriage(Guid id, [FromBody] TriageRequest request)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Guid.TryParse(userIdStr, out var nurseUserId);

        try
        {
            var token = await _nurseService.RecordVitalsAndTriageAsync(id, nurseUserId, request);
            await _notificationService.NotifyTriageCompletedAsync(token);
            return Ok(token);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An internal error occurred while recording vital signs." });
        }
    }

    /// <summary>
    /// View recorded vitals and clinical assessment for a token (GET /api/nurse/triage/{id}).
    /// </summary>
    [Authorize(Roles = "Nurse,Doctor,Admin")]
    [HttpGet("triage/{id:guid}")]
    public async Task<IActionResult> GetTriageAssessment(Guid id)
    {
        var result = await _nurseService.GetTriageAssessmentAsync(id);
        if (result == null)
        {
            return NotFound(new { message = "No triage assessment found for this token." });
        }
        return Ok(result);
    }

    #endregion
}
