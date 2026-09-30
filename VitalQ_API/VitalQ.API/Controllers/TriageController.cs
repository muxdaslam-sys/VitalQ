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
    private readonly ITriageService _triageService;

    public TriageController(ITriageService triageService)
    {
        _triageService = triageService;
    }

    /// <summary>
    /// Submit vitals + NursingStationId, compute triage level, move token to Waiting (Role: Nurse, Admin).
    /// </summary>
    [HttpPost("tokens/{id}/triage")]
    public async Task<IActionResult> SubmitTriage(Guid id, [FromBody] TriageRequest request)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Guid.TryParse(userIdStr, out var nurseUserId);

        var token = await _triageService.RecordTriageAsync(id, nurseUserId, request);
        return Ok(token);
    }
}
