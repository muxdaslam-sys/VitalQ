using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VitalQ.BusinessLogic.Interfaces;
using VitalQ.Entities.DTOs;

namespace VitalQ.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PatientsController : ControllerBase
{
    private readonly IPatientService _patientService;

    public PatientsController(IPatientService patientService)
    {
        _patientService = patientService;
    }

    /// <summary>
    /// 1. Self-Registration (Public: anyone can register from mobile app/web)
    /// POST /api/patients/self-register
    /// </summary>
    [AllowAnonymous]
    [HttpPost("self-register")]
    public async Task<IActionResult> SelfRegister([FromBody] SelfRegisterRequest request)
    {
        try
        {
            var result = await _patientService.SelfRegisterAsync(request);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// 2. Add Family Member (Role: Patient adds child/spouse under their account)
    /// POST /api/patients/family
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
    /// 3. Get My Family Members (Role: Patient lists all family profiles to pick who to book for)
    /// GET /api/patients/family
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
    /// 4. Walk-In Registration (Role: Nurse or Admin registers patient arriving at clinic)
    /// POST /api/patients/walk-in
    /// </summary>
    [Authorize(Roles = "Nurse,Admin")]
    [HttpPost("walk-in")]
    public async Task<IActionResult> WalkInRegister([FromBody] WalkInRegisterRequest request)
    {
        var result = await _patientService.WalkInRegisterAsync(request);
        return Ok(result);
    }

    /// <summary>
    /// 5. Search Patients (Role: Nurse, Admin, Doctor searches by Phone/Name/MRN)
    /// GET /api/patients/search?query=9876543210
    /// </summary>
    [Authorize(Roles = "Nurse,Admin,Doctor")]
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string query)
    {
        var results = await _patientService.SearchPatientsAsync(query);
        return Ok(results);
    }
}
