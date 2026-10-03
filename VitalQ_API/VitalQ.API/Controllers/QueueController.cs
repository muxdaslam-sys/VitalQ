using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using VitalQ.API.Hubs;
using VitalQ.BusinessLogic.Interfaces;
using VitalQ.Entities.DTOs;

namespace VitalQ.API.Controllers;

[Authorize(Roles = "Doctor,Admin,Nurse")]
[ApiController]
[Route("api")]
public class QueueController : ControllerBase
{
    private readonly IQueueService _queueService;
    private readonly IHubContext<QueueHub> _hubContext;

    public QueueController(IQueueService queueService, IHubContext<QueueHub> hubContext)
    {
        _queueService = queueService;
        _hubContext = hubContext;
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

        var doctor = await _queueService.GetDoctorByUserIdAsync(userId);
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
        var queue = await _queueService.GetDoctorQueueAsync(doctorId);
        return Ok(queue);
    }

    /// <summary>
    /// Gets the patient currently inside the consultation room (Role: Doctor, Admin).
    /// GET /api/queue/doctor/{doctorId}/current
    /// </summary>
    [HttpGet("queue/doctor/{doctorId}/current")]
    public async Task<IActionResult> GetCurrentCalled(Guid doctorId)
    {
        var token = await _queueService.GetCurrentCalledPatientAsync(doctorId);
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
            var token = await _queueService.CallNextPatientAsync(doctorId);
            if (token == null)
            {
                return NotFound(new { message = "No waiting patients in queue." });
            }

            // Real-Time SignalR Broadcasts:
            // 1. Notify the patient directly so their phone/tracker displays "Proceed to Room" and plays audio chime
            await _hubContext.Clients.Group($"patient-{token.Id}").SendAsync("PatientCalled", token);
            // 2. Notify the doctor's consultation console
            await _hubContext.Clients.Group($"doctor-{doctorId}").SendAsync("QueueUpdated", token);
            // 3. Notify the department waiting-room display
            await _hubContext.Clients.Group($"dept-{token.DepartmentId}").SendAsync("QueueUpdated", token);

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
            var token = await _queueService.SkipPatientAsync(id, doctorUserId);

            // SignalR Broadcasts
            await _hubContext.Clients.Group($"doctor-{token.DoctorId}").SendAsync("QueueUpdated", token);
            await _hubContext.Clients.Group($"dept-{token.DepartmentId}").SendAsync("QueueUpdated", token);
            await _hubContext.Clients.Group($"patient-{token.Id}").SendAsync("QueueUpdated", token);

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
            var token = await _queueService.RequeuePatientAsync(id, doctorUserId);

            // SignalR Broadcasts
            await _hubContext.Clients.Group($"doctor-{token.DoctorId}").SendAsync("QueueUpdated", token);
            await _hubContext.Clients.Group($"dept-{token.DepartmentId}").SendAsync("QueueUpdated", token);
            await _hubContext.Clients.Group($"patient-{token.Id}").SendAsync("QueueUpdated", token);

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
            var token = await _queueService.CompleteConsultationAsync(id, doctorUserId, request);

            // SignalR Broadcasts
            await _hubContext.Clients.Group($"doctor-{token.DoctorId}").SendAsync("QueueUpdated", token);
            await _hubContext.Clients.Group($"dept-{token.DepartmentId}").SendAsync("QueueUpdated", token);
            await _hubContext.Clients.Group($"patient-{token.Id}").SendAsync("ConsultationCompleted", token);

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
