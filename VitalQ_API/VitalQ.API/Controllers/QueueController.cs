using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VitalQ.BusinessLogic.Interfaces;
using VitalQ.Entities.DTOs;

namespace VitalQ.API.Controllers;

[Authorize(Roles = "Doctor,Admin,Nurse")]
[ApiController]
[Route("api")]
public class QueueController : ControllerBase
{
    private readonly IDoctorService _doctorService;
    private readonly IQueueNotificationService _notificationService;

    public QueueController(IDoctorService doctorService, IQueueNotificationService notificationService)
    {
        _doctorService = doctorService;
        _notificationService = notificationService;
    }

    /// <summary>
    /// Get the doctor profile for the current logged-in user (Role: Doctor, Admin).
    /// GET /api/doctors/me
    /// </summary>
    [HttpGet("doctors/me")]
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
    /// Live priority-sorted queue for a doctor (Role: Doctor, Admin, Nurse).
    /// GET /api/queue/doctor/{doctorId}
    /// </summary>
    [HttpGet("queue/doctor/{doctorId}")]
    public async Task<IActionResult> GetDoctorQueue(Guid doctorId)
    {
        var queue = await _doctorService.GetMyQueueAsync(doctorId);
        return Ok(queue);
    }

    /// <summary>
    /// Gets the patient currently inside the consultation room (Role: Doctor, Admin).
    /// GET /api/queue/doctor/{doctorId}/current
    /// </summary>
    [HttpGet("queue/doctor/{doctorId}/current")]
    public async Task<IActionResult> GetCurrentCalled(Guid doctorId)
    {
        var token = await _doctorService.GetCurrentCalledPatientAsync(doctorId);
        return Ok(token);
    }

    /// <summary>
    /// Picks top-priority Waiting token for this doctor, moves to Called. Guarded by RowVersion (Role: Doctor, Admin).
    /// POST /api/queue/doctor/{doctorId}/call-next
    /// </summary>
    [HttpPost("queue/doctor/{doctorId}/call-next")]
    public async Task<IActionResult> CallNext(Guid doctorId)
    {
        try
        {
            var token = await _doctorService.CallNextPatientAsync(doctorId);
            if (token == null)
            {
                return NotFound(new { message = "No waiting patients in queue." });
            }

            // Real-Time Notification:
            // Pushes directly to the called Patient's Mobile/PC screen (chime audio + room number)
            // and updates Doctor's PC console
            await _notificationService.NotifyPatientCalledAsync(token);

            return Ok(token);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Holds an absent patient; token moves to Skipped (Role: Doctor, Admin, Nurse).
    /// POST /api/tokens/{id}/skip
    /// </summary>
    [HttpPost("tokens/{id}/skip")]
    public async Task<IActionResult> SkipPatient(Guid id)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Guid.TryParse(userIdStr, out var doctorUserId);

        try
        {
            var token = await _doctorService.SkipPatientAsync(id, doctorUserId);

            // Notify Doctor console that queue shifted
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
    /// Re-activates a skipped patient back into the Waiting queue when they return (Role: Doctor, Admin, Nurse).
    /// POST /api/tokens/{id}/requeue
    /// </summary>
    [HttpPost("tokens/{id}/requeue")]
    public async Task<IActionResult> RequeuePatient(Guid id)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Guid.TryParse(userIdStr, out var doctorUserId);

        try
        {
            var token = await _doctorService.RequeuePatientAsync(id, doctorUserId);

            // Notify Doctor console that queue shifted
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
    /// Closes visit, stamps CompletedAtUtc, and saves ConsultationNotes (Role: Doctor, Admin).
    /// POST /api/tokens/{id}/complete
    /// </summary>
    [HttpPost("tokens/{id}/complete")]
    public async Task<IActionResult> CompleteConsultation(Guid id, [FromBody] CompleteConsultationRequest request)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Guid.TryParse(userIdStr, out var doctorUserId);

        try
        {
            var token = await _doctorService.CompleteConsultationAsync(id, doctorUserId, request);

            // Notify Doctor console that consultation finished
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
