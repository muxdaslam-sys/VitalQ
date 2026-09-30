using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using VitalQ.API.Hubs;
using VitalQ.BusinessLogic.Interfaces;
using VitalQ.DataAccess;
using VitalQ.Entities.DTOs;

namespace VitalQ.API.Controllers;

[ApiController]
[Route("api")]
public class BookingController : ControllerBase
{
    private readonly IBookingService _bookingService;
    private readonly VitalQDbContext _context;
    private readonly IHubContext<QueueHub> _hubContext;

    public BookingController(IBookingService bookingService, VitalQDbContext context, IHubContext<QueueHub> hubContext)
    {
        _bookingService = bookingService;
        _context = context;
        _hubContext = hubContext;
    }

    /// <summary>
    /// 1. Active clinical departments (Public)
    /// GET /api/departments
    /// </summary>
    [AllowAnonymous]
    [HttpGet("departments")]
    public async Task<IActionResult> GetDepartments()
    {
        var departments = await _bookingService.GetActiveDepartmentsAsync();
        return Ok(departments);
    }

    /// <summary>
    /// 2. Available doctors in a department (Public)
    /// GET /api/departments/{id}/doctors
    /// </summary>
    [AllowAnonymous]
    [HttpGet("departments/{id}/doctors")]
    public async Task<IActionResult> GetDoctorsByDepartment(Guid id)
    {
        var doctors = await _bookingService.GetAvailableDoctorsAsync(id);
        return Ok(doctors);
    }

    /// <summary>
    /// 3. Book an appointment slot -> generates atomic token (Patient, Nurse, Admin)
    /// POST /api/bookings
    /// </summary>
    [Authorize(Roles = "Patient,Nurse,Admin")]
    [HttpPost("bookings")]
    public async Task<IActionResult> BookToken([FromBody] BookTokenRequest request)
    {
        // If patient ID was not provided, default to the authenticated user's own profile
        if (request.PatientId == Guid.Empty)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(userIdStr, out var currentUserId))
            {
                var selfPatient = await _context.Patients.FirstOrDefaultAsync(p => p.UserId == currentUserId);
                if (selfPatient != null)
                {
                    request.PatientId = selfPatient.Id;
                }
            }
        }

        if (request.PatientId == Guid.Empty)
        {
            return BadRequest(new { message = "PatientId is required." });
        }

        try
        {
            var token = await _bookingService.BookTokenAsync(request);

            // Real-Time SignalR push to the Doctor's room display
            await _hubContext.Clients.Group($"doctor-{token.DoctorId}").SendAsync("TokenBooked", token);

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
    /// 4. Current caller's active token today, if any (Patient, Admin)
    /// GET /api/bookings/my-token
    /// </summary>
    [Authorize(Roles = "Patient,Admin")]
    [HttpGet("bookings/my-token")]
    public async Task<IActionResult> GetMyToken([FromQuery] Guid? patientId)
    {
        var targetId = patientId ?? Guid.Empty;

        // If not specified in query, default to authenticated user's patient profile
        if (targetId == Guid.Empty)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(userIdStr, out var currentUserId))
            {
                var selfPatient = await _context.Patients.FirstOrDefaultAsync(p => p.UserId == currentUserId);
                if (selfPatient != null) targetId = selfPatient.Id;
            }
        }

        if (targetId == Guid.Empty)
        {
            return NotFound(new { message = "No patient profile found for current user." });
        }

        var token = await _bookingService.GetPatientActiveTokenAsync(targetId);
        if (token == null)
        {
            return NotFound(new { message = "No active token found for today." });
        }
        return Ok(token);
    }
}
