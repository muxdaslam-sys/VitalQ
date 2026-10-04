using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VitalQ.BusinessLogic.Interfaces;
using VitalQ.Entities.DTOs;

namespace VitalQ.API.Controllers;

/// <summary>
/// Dedicated Nurse Station & Reception Desk Controller on PC.
/// Handles patient lookup, walk-in registration, counter token booking, and vitals triage.
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

    /// <summary>
    /// Search patients by Phone, MRN, or Name for intake at the clinic desk.
    /// </summary>
    [HttpGet("patients/search")]
    public async Task<IActionResult> SearchPatients([FromQuery] string query)
    {
        var result = await _nurseService.SearchPatientsAsync(query);
        return Ok(result);
    }

    /// <summary>
    /// Register a walk-in patient in-person at the clinic reception desk.
    /// </summary>
    [HttpPost("patients/walk-in")]
    public async Task<IActionResult> RegisterWalkIn([FromBody] WalkInRegisterRequest request)
    {
        try
        {
            var result = await _nurseService.RegisterWalkInPatientAsync(request);
            return Ok(result);
        }
        catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Issue a walk-in queue slip directly at the counter.
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
    }

    /// <summary>
    /// List patients booked today awaiting vital signs intake.
    /// </summary>
    [HttpGet("triage/pending")]
    public async Task<IActionResult> GetPendingTriage([FromQuery] Guid? departmentId = null)
    {
        var result = await _nurseService.GetPendingTriageQueueAsync(departmentId);
        return Ok(result);
    }

    /// <summary>
    /// Record vitals (BP, SpO2, HR, Temp, Pain) and assign CTAS urgency level.
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
    }

    /// <summary>
    /// View recorded vitals and clinical assessment for a token.
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
}
