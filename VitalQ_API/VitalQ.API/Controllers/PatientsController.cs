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

            // Set HttpOnly cookie for seamless token refresh
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
        try
        {
            var result = await _patientService.WalkInRegisterAsync(request);
            return Ok(result);
        }
        catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException)
        {
            return BadRequest(new { message = ex.Message });
        }
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

    /// <summary>
    /// 6. Get Patient Profile by ID (Role: Nurse, Admin, Doctor, Patient)
    /// GET /api/patients/{id:guid}
    /// </summary>
    [Authorize(Roles = "Nurse,Admin,Doctor,Patient")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById([FromRoute] Guid id)
    {
        // If caller is a Patient, ensure they can only view their own or their family's records
        if (User.IsInRole("Patient"))
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var currentUserId))
            {
                return Unauthorized();
            }

            var family = await _patientService.GetMyFamilyMembersAsync(currentUserId);
            if (!family.Any(f => f.Id == id))
            {
                return Forbid();
            }
        }

        var patient = await _patientService.GetPatientByIdAsync(id);
        if (patient == null)
        {
            return NotFound(new { message = $"Patient with ID {id} was not found." });
        }
        return Ok(patient);
    }

    /// <summary>
    /// 7. Update Patient Profile (Role: Patient, Nurse, Admin)
    /// PUT /api/patients/{id:guid}
    /// </summary>
    [Authorize(Roles = "Patient,Nurse,Admin")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdatePatientRequest request)
    {
        // If caller is a Patient, ensure they can only update their own or their family's records
        if (User.IsInRole("Patient"))
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var currentUserId))
            {
                return Unauthorized();
            }

            var family = await _patientService.GetMyFamilyMembersAsync(currentUserId);
            if (!family.Any(f => f.Id == id))
            {
                return Forbid();
            }
        }

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

    /// <summary>
    /// 8. Get Patient Visit History (Role: Patient, Nurse, Doctor, Admin)
    /// GET /api/patients/{id:guid}/history
    /// </summary>
    [Authorize(Roles = "Patient,Nurse,Doctor,Admin")]
    [HttpGet("{id:guid}/history")]
    public async Task<IActionResult> GetVisitHistory([FromRoute] Guid id)
    {
        if (User.IsInRole("Patient"))
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var currentUserId))
            {
                return Unauthorized();
            }

            var family = await _patientService.GetMyFamilyMembersAsync(currentUserId);
            if (!family.Any(f => f.Id == id))
            {
                return Forbid();
            }
        }

        var history = await _patientService.GetPatientVisitHistoryAsync(id);
        return Ok(history);
    }

    /// <summary>
    /// 9. Get Current Logged-in Patient's Visit History (Role: Patient)
    /// GET /api/patients/my-history
    /// </summary>
    [Authorize(Roles = "Patient")]
    [HttpGet("my-history")]
    public async Task<IActionResult> GetMyVisitHistory()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var currentUserId))
            return Unauthorized();

        var family = await _patientService.GetMyFamilyMembersAsync(currentUserId);
        if (!family.Any())
            return NotFound(new { message = "No patient profiles found for this account." });

        // Fetch history for every family member in parallel and merge
        var historyTasks = family
            .Select(member => _patientService.GetPatientVisitHistoryAsync(member.Id));

        var results = await Task.WhenAll(historyTasks);

        var combined = results
            .SelectMany(h => h)
            .OrderByDescending(h => h.BookedAtUtc)
            .ToList();

        return Ok(combined);
    }
}

