using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VitalQ.BusinessLogic.Interfaces;
using VitalQ.Entities.DTOs;

namespace VitalQ.API.Controllers;

[Authorize(Roles = "Nurse,Admin")]
[ApiController]
[Route("api")]
public class TriageController : ControllerBase
{
    private readonly INurseService _nurseService;
    private readonly IQueueNotificationService _notificationService;

    public TriageController(INurseService nurseService, IQueueNotificationService notificationService)
    {
        _nurseService = nurseService;
        _notificationService = notificationService;
    }

    /// <summary>
    /// Submit vitals, compute triage level, and move token to Waiting (Role: Nurse, Admin on PC).
    /// POST /api/tokens/{id}/triage
    /// </summary>
    [HttpPost("tokens/{id}/triage")]
    public async Task<IActionResult> SubmitTriage(Guid id, [FromBody] TriageRequest request)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Guid.TryParse(userIdStr, out var nurseUserId);

        try
        {
            var token = await _nurseService.RecordVitalsAndTriageAsync(id, nurseUserId, request);

            // Real-Time Broadcast: Notifies Doctor PC console that a patient has been triaged
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
    /// Pending tokens waiting for nurse triage intake today (Role: Nurse, Admin on PC).
    /// GET /api/triage/pending?departmentId={guid}
    /// </summary>
    [HttpGet("triage/pending")]
    public async Task<IActionResult> GetPendingTriage([FromQuery] Guid? departmentId = null)
    {
        var tokens = await _nurseService.GetPendingTriageQueueAsync(departmentId);
        return Ok(tokens);
    }

    /// <summary>
    /// View recorded triage vitals and clinical assessment for a token (Role: Nurse, Doctor, Admin).
    /// GET /api/tokens/{id}/triage
    /// </summary>
    [Authorize(Roles = "Nurse,Doctor,Admin")]
    [HttpGet("tokens/{id}/triage")]
    public async Task<IActionResult> GetTriageAssessment(Guid id)
    {
        var assessment = await _nurseService.GetTriageAssessmentAsync(id);
        if (assessment == null)
        {
            return NotFound(new { message = "No triage assessment found for this token." });
        }
        return Ok(assessment);
    }
}
