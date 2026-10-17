using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VitalQ.BusinessLogic.Interfaces;
using VitalQ.Entities.DTOs;

namespace VitalQ.API.Controllers;

/// <summary>
/// Dedicated Doctor Consultation Room Controller on PC.
/// Exposes endpoints for doctor shift metrics, live priority queue navigation,
/// patient summoning, skipped patient management, and consultation conclusion.
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

    #region 1. Doctor Profile & Duty Status

    /// <summary>
    /// Retrieves the clinical doctor profile for the authenticated session (GET /api/doctor/me).
    /// </summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentDoctor()
    {
        var doctor = await GetCallerDoctorProfileAsync();
        if (doctor == null)
        {
            return NotFound(new { message = "No doctor profile associated with this account." });
        }

        return Ok(doctor);
    }

    /// <summary>
    /// Aggregates real-time shift performance metrics for the doctor console (GET /api/doctor/dashboard/stats).
    /// </summary>
    [HttpGet("dashboard/stats")]
    public async Task<IActionResult> GetDashboardStats([FromQuery] Guid? doctorId = null)
    {
        var targetDoctorId = await ResolveTargetDoctorIdAsync(doctorId);
        if (targetDoctorId == null)
        {
            return Forbid();
        }

        try
        {
            var stats = await _doctorService.GetDashboardStatsAsync(targetDoctorId.Value);
            return Ok(stats);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Updates physician availability status between Available, Busy, and OnLeave (PUT /api/doctor/status).
    /// </summary>
    [HttpPut("status")]
    public async Task<IActionResult> UpdateStatus([FromBody] UpdateDoctorStatusRequest request, [FromQuery] Guid? doctorId = null)
    {
        var targetDoctorId = await ResolveTargetDoctorIdAsync(doctorId);
        if (targetDoctorId == null)
        {
            return Forbid();
        }

        try
        {
            var updatedDoctor = await _doctorService.UpdateDoctorStatusAsync(targetDoctorId.Value, request.Status);
            await _notificationService.NotifyDoctorStatusChangedAsync(targetDoctorId.Value, request.Status);
            return Ok(updatedDoctor);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    #endregion

    #region 2. Live Consultation Queue & Summoning

    /// <summary>
    /// Live priority-sorted queue for a doctor's consultation room (GET /api/doctor/queue/{doctorId}).
    /// Ranked dynamically by PriorityScore (CTAS base weight + anti-starvation wait-time aging).
    /// </summary>
    [HttpGet("queue/{doctorId:guid}")]
    public async Task<IActionResult> GetDoctorQueue(Guid doctorId)
    {
        if (!await IsAuthorizedForDoctorAsync(doctorId))
        {
            return Forbid();
        }

        var queue = await _doctorService.GetMyQueueAsync(doctorId);
        return Ok(queue);
    }

    /// <summary>
    /// Retrieves the patient currently called into the doctor's consultation chamber (GET /api/doctor/current/{doctorId}).
    /// </summary>
    [HttpGet("current/{doctorId:guid}")]
    public async Task<IActionResult> GetCurrentCalled(Guid doctorId)
    {
        if (!await IsAuthorizedForDoctorAsync(doctorId))
        {
            return Forbid();
        }

        var token = await _doctorService.GetCurrentCalledPatientAsync(doctorId);
        return Ok(token);
    }

    /// <summary>
    /// Advances queue: summons highest-priority Waiting patient into the room (POST /api/doctor/call-next/{doctorId}).
    /// Pushes real-time melodic audio chime and room assignment to patient screen & doctor console.
    /// </summary>
    [HttpPost("call-next/{doctorId:guid}")]
    public async Task<IActionResult> CallNext(Guid doctorId)
    {
        if (!await IsAuthorizedForDoctorAsync(doctorId))
        {
            return Forbid();
        }

        try
        {
            var token = await _doctorService.CallNextPatientAsync(doctorId);
            if (token == null)
            {
                return NotFound(new { message = "No waiting patients in queue." });
            }

            // Real-Time Notification: Pushes chime audio and room assignment to patient screen & doctor console
            await _notificationService.NotifyPatientCalledAsync(token);

            return Ok(token);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Re-broadcasts the melodic summon chime and visual call notification for an already-called patient (POST /api/doctor/tokens/{id}/summon).
    /// </summary>
    [HttpPost("tokens/{id:guid}/summon")]
    public async Task<IActionResult> ResummonPatient(Guid id)
    {
        var callerDoctor = await GetCallerDoctorProfileAsync();
        var doctorId = callerDoctor?.Id ?? Guid.Empty;

        try
        {
            var token = await _doctorService.ResummonPatientAsync(id, doctorId);
            if (token == null)
            {
                return NotFound(new { message = "Token was not found in Called status." });
            }

            await _notificationService.NotifyPatientCalledAsync(token);
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

    #endregion

    #region 3. Consultation Workflow & State Transitions

    /// <summary>
    /// Holds an absent patient; token status transitions to Skipped (POST /api/doctor/tokens/{id}/skip).
    /// </summary>
    [HttpPost("tokens/{id:guid}/skip")]
    public async Task<IActionResult> SkipPatient(Guid id)
    {
        var doctorUserId = GetCallerUserId();

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
    /// Re-activates an absent patient back into the active Waiting queue (POST /api/doctor/tokens/{id}/requeue).
    /// Accessible by Doctors, Nurses (at triage desk), and Admins.
    /// </summary>
    [Authorize(Roles = "Doctor,Nurse,Admin")]
    [HttpPost("tokens/{id:guid}/requeue")]
    public async Task<IActionResult> RequeuePatient(Guid id)
    {
        var callerUserId = GetCallerUserId();

        try
        {
            var token = await _doctorService.RequeuePatientAsync(id, callerUserId);
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
    /// Concludes consultation, stamps CompletedAtUtc, and saves clinical diagnosis notes (POST /api/doctor/tokens/{id}/complete).
    /// </summary>
    [HttpPost("tokens/{id:guid}/complete")]
    public async Task<IActionResult> CompleteConsultation(Guid id, [FromBody] CompleteConsultationRequest request)
    {
        var doctorUserId = GetCallerUserId();

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

    #endregion

    #region 4. Patient Encounter History & Shift Audit

    /// <summary>
    /// Retrieves consultations completed today by the logged-in doctor in chronological order (GET /api/doctor/history).
    /// </summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetCompletedHistoryToday([FromQuery] Guid? doctorId = null)
    {
        var targetDoctorId = await ResolveTargetDoctorIdAsync(doctorId);
        if (targetDoctorId == null)
        {
            return Forbid();
        }

        var history = await _doctorService.GetCompletedHistoryTodayAsync(targetDoctorId.Value);
        return Ok(history);
    }

    /// <summary>
    /// Retrieves previous hospital encounters and consultation notes for a patient (GET /api/doctor/patients/{patientId}/history).
    /// </summary>
    [HttpGet("patients/{patientId:guid}/history")]
    public async Task<IActionResult> GetPatientPastEncounters(Guid patientId)
    {
        var encounters = await _doctorService.GetPatientPastEncountersAsync(patientId);
        return Ok(encounters);
    }

    #endregion

    #region Internal Identity Helpers

    private Guid GetCallerUserId()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Guid.TryParse(userIdStr, out var userId);
        return userId;
    }

    private async Task<DoctorResponse?> GetCallerDoctorProfileAsync()
    {
        var userId = GetCallerUserId();
        if (userId == Guid.Empty) return null;
        return await _doctorService.GetDoctorByUserIdAsync(userId);
    }

    private async Task<Guid?> ResolveTargetDoctorIdAsync(Guid? requestedDoctorId)
    {
        if (User.IsInRole("Admin") && requestedDoctorId.HasValue)
        {
            return requestedDoctorId.Value;
        }

        var callerDoctor = await GetCallerDoctorProfileAsync();
        return callerDoctor?.Id;
    }

    private async Task<bool> IsAuthorizedForDoctorAsync(Guid targetDoctorId)
    {
        if (User.IsInRole("Admin")) return true;

        var callerDoctor = await GetCallerDoctorProfileAsync();
        return callerDoctor != null && callerDoctor.Id == targetDoctorId;
    }

    #endregion
}
