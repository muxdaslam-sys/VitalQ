using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VitalQ.BusinessLogic.Interfaces;

namespace VitalQ.API.Controllers;

/// <summary>
/// Public Controller for unauthenticated operations (guest slip tracking & directory lookups).
/// </summary>
[AllowAnonymous]
[ApiController]
[Route("api/public")]
public class PublicController : ControllerBase
{
    private readonly IPublicService _publicService;

    public PublicController(IPublicService publicService)
    {
        _publicService = publicService;
    }

    /// <summary>
    /// Active clinical departments (Cached 2 min).
    /// </summary>
    [HttpGet("departments")]
    public async Task<IActionResult> GetDepartments()
    {
        var result = await _publicService.GetActiveDepartmentsAsync();
        return Ok(result);
    }

    /// <summary>
    /// Available doctors on-duty by department (Cached 1 min).
    /// </summary>
    [HttpGet("departments/{id:guid}/doctors")]
    public async Task<IActionResult> GetDoctorsByDepartment(Guid id)
    {
        var result = await _publicService.GetAvailableDoctorsAsync(id);
        return Ok(result);
    }

    /// <summary>
    /// Live paper slip tracker for unregistered visitors on PC or Mobile.
    /// </summary>
    [HttpGet("track/{tokenNumber}")]
    public async Task<IActionResult> TrackToken(string tokenNumber)
    {
        if (string.IsNullOrWhiteSpace(tokenNumber))
        {
            return BadRequest(new { message = "Token number is required." });
        }

        var result = await _publicService.TrackTokenAsync(tokenNumber.Trim().ToUpper());
        if (result == null)
        {
            return NotFound(new { message = $"No active consultation slip found matching '{tokenNumber}' for today." });
        }

        return Ok(result);
    }
}
