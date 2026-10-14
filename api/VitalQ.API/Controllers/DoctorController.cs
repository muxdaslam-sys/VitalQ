using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VitalQ.BusinessLogic.Interfaces;
using VitalQ.Entities.DTOs;

namespace VitalQ.API.Controllers;

/// <summary>
/// Dedicated Doctor Consultation Room Controller on PC.
/// Handles priority queue viewing, calling next, skipping, requeueing, and completing consultations.
/// </summary>
[Authorize(Roles = "Doctor,Admin")]
[ApiController]
[Route("api/doctor")]
public class DoctorController : ControllerBase
{
    private readonly IDoctorService _doctorService;
    private readonly IQueueNotificationService _notificationService;

    public DoctorController(IDoctorService doctorService, IQueueNotificationService notificationService)
    {
        _doctorService = doctorService;
        _notificationService = notificationService;
    }

    /// <summary>
    /// Get the doctor profile for the current logged-in user.
    /// </summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentDoctor()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userId))
        {
            return Unauthorized();
        }

        var doctor = await _doctorService.GetDoctorByUserIdAsync(userId);
        if (doctor == null)
        {
            return NotFound(new { message = "No doctor profile associated with this account." });
        }

        return Ok(doctor);
    }

    /// <summary>
    /// Live priority-sorted queue for a doctor's PC console.
    /// </summary>
    [HttpGet("queue/{doctorId:guid}")]
    public async Task<IActionResult> GetDoctorQueue(Guid doctorId)
    {
        var queue = await _doctorService.GetMyQueueAsync(doctorId);
        return Ok(queue);
    }

    /// <summary>
    /// Gets the patient currently inside the consultation room.
    /// </summary>
    [HttpGet("current/{doctorId:guid}")]
    public async Task<IActionResult> GetCurrentCalled(Guid doctorId)
    {
        var token = await _doctorService.GetCurrentCalledPatientAsync(doctorId);
        return Ok(token);
    }

    /// <summary>
    /// Picks top-priority Waiting token, moves to Called, and notifies patient mobile/PC screen.
    /// </summary>
    [HttpPost("call-next/{doctorId:guid}")]
    public async Task<IActionResult> CallNext(Guid doctorId)
    {
        try
        {
            var token = await _doctorService.CallNextPatientAsync(doctorId);
            if (token == null)
            {
                return NotFound(new { message = "No waiting patients in queue." });
            }

            // Real-Time Notification: Pushes chime audio and room assignment to patient screen & doctor PC
            await _notificationService.NotifyPatientCalledAsync(token);

            return Ok(token);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Holds an absent patient; token moves to Skipped.
    /// </summary>
    [HttpPost("tokens/{id:guid}/skip")]
    public async Task<IActionResult> SkipPatient(Guid id)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Guid.TryParse(userIdStr, out var doctorUserId);

        try
        {
            var token = await _doctorService.SkipPatientAsync(id, doctorUserId);
            await _notificationService.NotifyQueueUpdatedAsync(token.DoctorId, token);
            return Ok(token);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Re-activates a skipped patient back into the Waiting queue when they arrive.
    /// </summary>
    [HttpPost("tokens/{id:guid}/requeue")]
    public async Task<IActionResult> RequeuePatient(Guid id)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Guid.TryParse(userIdStr, out var doctorUserId);

        try
        {
            var token = await _doctorService.RequeuePatientAsync(id, doctorUserId);
            await _notificationService.NotifyQueueUpdatedAsync(token.DoctorId, token);
            return Ok(token);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Closes visit, stamps CompletedAtUtc, and saves ConsultationNotes.
    /// </summary>
    [HttpPost("tokens/{id:guid}/complete")]
    public async Task<IActionResult> CompleteConsultation(Guid id, [FromBody] CompleteConsultationRequest request)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Guid.TryParse(userIdStr, out var doctorUserId);

        try
        {
            var token = await _doctorService.CompleteConsultationAsync(id, doctorUserId, request);
            await _notificationService.NotifyQueueUpdatedAsync(token.DoctorId, token);
            return Ok(token);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
