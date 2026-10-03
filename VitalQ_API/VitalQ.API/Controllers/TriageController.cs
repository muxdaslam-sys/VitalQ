using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using VitalQ.API.Hubs;
using VitalQ.BusinessLogic.Interfaces;
using VitalQ.Entities.DTOs;

namespace VitalQ.API.Controllers;

[Authorize(Roles = "Nurse,Admin")]
[ApiController]
[Route("api")]
public class TriageController : ControllerBase
{
    private readonly ITriageService _triageService;
    private readonly IHubContext<QueueHub> _hubContext;

    public TriageController(ITriageService triageService, IHubContext<QueueHub> hubContext)
    {
        _triageService = triageService;
        _hubContext = hubContext;
    }

    /// <summary>
    /// Submit vitals + NursingStationId, compute triage level, move token to Waiting (Role: Nurse, Admin).
    /// POST /api/tokens/{id}/triage
    /// </summary>
    [HttpPost("tokens/{id}/triage")]
    public async Task<IActionResult> SubmitTriage(Guid id, [FromBody] TriageRequest request)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Guid.TryParse(userIdStr, out var nurseUserId);

        try
        {
            var token = await _triageService.RecordTriageAsync(id, nurseUserId, request);

            // Real-Time SignalR Broadcasts:
            // 1. Notify the doctor that a patient has been triaged and is waiting
            await _hubContext.Clients.Group($"doctor-{token.DoctorId}").SendAsync("QueueUpdated", token);
            // 2. Notify the department waiting room display
            await _hubContext.Clients.Group($"dept-{token.DepartmentId}").SendAsync("QueueUpdated", token);
            // 3. Notify the patient live tracker
            await _hubContext.Clients.Group($"patient-{token.Id}").SendAsync("QueueUpdated", token);

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
    /// Pending tokens waiting for nurse triage intake today (Role: Nurse, Admin).
    /// GET /api/triage/pending?departmentId={guid}
    /// </summary>
    [HttpGet("triage/pending")]
    public async Task<IActionResult> GetPendingTriage([FromQuery] Guid? departmentId = null)
    {
        var tokens = await _triageService.GetPendingTriageTokensAsync(departmentId);
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
        var assessment = await _triageService.GetTriageAssessmentByTokenIdAsync(id);
        if (assessment == null)
        {
            return NotFound(new { message = "No triage assessment found for this token." });
        }
        return Ok(assessment);
    }
}
