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
    /// Active departments list (Public).
    /// </summary>
    [HttpGet("departments")]
    public async Task<IActionResult> GetDepartments()
    {
        var departments = await _bookingService.GetActiveDepartmentsAsync();
        return Ok(departments);
    }

    /// <summary>
    /// Available doctors in a department (Public).
    /// </summary>
    [HttpGet("departments/{id}/doctors")]
    public async Task<IActionResult> GetDoctorsByDepartment(Guid id)
    {
        var doctors = await _bookingService.GetAvailableDoctorsAsync(id);
        return Ok(doctors);
    }

    /// <summary>
    /// Book a slot -> generates a token (Role: Patient).
    /// </summary>
    [HttpPost("bookings")]
    public async Task<IActionResult> BookToken([FromBody] BookTokenRequest request)
    {
        // TODO: Extract patientId from JWT claims
        var patientId = Guid.Empty;
        var token = await _bookingService.BookTokenAsync(patientId, request);
        return Ok(token);
    }

    /// <summary>
    /// Current caller's active token, if any (Role: Patient).
    /// </summary>
    [HttpGet("bookings/my-token")]
    public async Task<IActionResult> GetMyToken()
    {
        var patientId = Guid.Empty;
        var token = await _bookingService.GetPatientActiveTokenAsync(patientId);
        if (token == null) return NotFound(new { message = "No active token found" });
        return Ok(token);
    }
}
