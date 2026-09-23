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
    private readonly IBookingService _bookingService;

    public TriageController(ITriageService triageService, IBookingService bookingService)
    {
        _triageService = triageService;
        _bookingService = bookingService;
    }

    /// <summary>
    /// Search arriving patient by token number or phone number (Role: Nurse, Admin).
    /// </summary>
    [HttpGet("patients/search")]
    public async Task<IActionResult> SearchPatients([FromQuery] string query)
    {
        var results = await _triageService.SearchPatientsAsync(query);
        return Ok(results);
    }

    /// <summary>
    /// Emergency walk-in token creation without prior booking (Role: Nurse, Admin).
    /// </summary>
    [HttpPost("tokens/walk-in")]
    public async Task<IActionResult> CreateWalkInToken([FromBody] WalkInTokenRequest request)
    {
        var token = await _bookingService.CreateWalkInTokenAsync(request);
        return Ok(token);
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
