using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VitalQ.BusinessLogic.Interfaces;
using VitalQ.Entities.DTOs;

namespace VitalQ.API.Controllers;

[ApiController]
[Route("api")]
public class BookingController : ControllerBase
{
    private readonly IBookingService _bookingService;

    public BookingController(IBookingService bookingService)
    {
        _bookingService = bookingService;
    }

    /// <summary>
    /// Active departments list (Public - unauthenticated guests can view).
    /// </summary>
    [AllowAnonymous]
    [HttpGet("departments")]
    public async Task<IActionResult> GetDepartments()
    {
        var departments = await _bookingService.GetActiveDepartmentsAsync();
        return Ok(departments);
    }

    /// <summary>
    /// Available doctors in a department (Public - unauthenticated guests can view).
    /// </summary>
    [AllowAnonymous]
    [HttpGet("departments/{id}/doctors")]
    public async Task<IActionResult> GetDoctorsByDepartment(Guid id)
    {
        var doctors = await _bookingService.GetAvailableDoctorsAsync(id);
        return Ok(doctors);
    }

    /// <summary>
    /// Book a slot -> generates a token (Role: Patient, Admin).
    /// </summary>
    [Authorize(Roles = "Patient,Admin")]
    [HttpPost("bookings")]
    public async Task<IActionResult> BookToken([FromBody] BookTokenRequest request)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Guid.TryParse(userIdStr, out var patientId);

        var token = await _bookingService.BookTokenAsync(patientId, request);
        return Ok(token);
    }

    /// <summary>
    /// Current caller's active token, if any (Role: Patient, Admin).
    /// </summary>
    [Authorize(Roles = "Patient,Admin")]
    [HttpGet("bookings/my-token")]
    public async Task<IActionResult> GetMyToken()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Guid.TryParse(userIdStr, out var patientId);

        var token = await _bookingService.GetPatientActiveTokenAsync(patientId);
        if (token == null) return NotFound(new { message = "No active token found" });
        return Ok(token);
    }
}
