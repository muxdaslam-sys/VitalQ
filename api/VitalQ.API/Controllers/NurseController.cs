using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VitalQ.BusinessLogic.Interfaces;
using VitalQ.Entities.DTOs;

namespace VitalQ.API.Controllers;

/// <summary>
/// Exposes nurse station endpoints: patient lookup, walk-in registration, token issuance, and clinical triage.
/// </summary>
[Authorize(Roles = "Nurse,Admin")]
[ApiController]
[Route("api/nurse")]
public class NurseController : ControllerBase
{
    private readonly INurseService _nurseService;
    private readonly IDoctorService _doctorService;
    private readonly IQueueNotificationService _notificationService;

    public NurseController(INurseService nurseService, IDoctorService doctorService, IQueueNotificationService notificationService)
    {
        _nurseService = nurseService;
        _doctorService = doctorService;
        _notificationService = notificationService;
    }

    #region 1. Patient Lookup & Identification

    /// <summary>
    /// Search patients by Token Number, MRN, Phone, or Name for intake at the clinic desk (GET /api/nurse/patients/search?query=...).
    /// </summary>
    [HttpGet("patients/search")]
    public async Task<IActionResult> SearchPatients([FromQuery] string query)
    {
        var result = await _nurseService.SearchPatientsAsync(query);
        return Ok(result);
    }

    #endregion

    #region 2. Clinical Triage & Vital Signs Intake

    /// <summary>
    /// List patients booked today awaiting vital signs intake (GET /api/nurse/triage/pending?departmentId=...&nursingStationId=...).
    /// </summary>
    [HttpGet("triage/pending")]
    public async Task<IActionResult> GetPendingTriage([FromQuery] Guid? departmentId = null, [FromQuery] Guid? nursingStationId = null)
    {
        // Enforce SBAC: If the caller is a Nurse, lock them exclusively to their assigned station
        if (User.IsInRole("Nurse"))
        {
            var stationClaim = User.FindFirst("nursingStationId")?.Value;
            if (Guid.TryParse(stationClaim, out var assignedStationId))
            {
                nursingStationId = assignedStationId;
            }
            else
            {
                return Forbid();
            }
        }

        var result = await _nurseService.GetPendingTriageQueueAsync(departmentId, nursingStationId);
        return Ok(result);
    }

    /// <summary>
    /// Record vitals (BP, SpO2, HR, Temp, Pain) and assign CTAS urgency level (POST /api/nurse/triage/{id}).
    /// </summary>
    [HttpPost("triage/{id:guid}")]
    public async Task<IActionResult> SubmitTriage(Guid id, [FromBody] TriageRequest request)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Guid.TryParse(userIdStr, out var nurseUserId);

        // Enforce SBAC: Stamp the assessment with the nurse's assigned station
        if (User.IsInRole("Nurse"))
        {
            var stationClaim = User.FindFirst("nursingStationId")?.Value;
            if (Guid.TryParse(stationClaim, out var assignedStationId))
            {
                request.NursingStationId = assignedStationId;
            }
        }

        try
        {
            var token = await _nurseService.RecordVitalsAndTriageAsync(id, nurseUserId, request);
            await _notificationService.NotifyTriageCompletedAsync(token);
            return Ok(token);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An internal error occurred while recording vital signs." });
        }
    }

    /// <summary>
    /// View recorded vitals and clinical assessment for a token (GET /api/nurse/triage/{id}).
    /// </summary>
    [Authorize(Roles = "Nurse,Doctor,Admin")]
    [HttpGet("triage/{id:guid}")]
    public async Task<IActionResult> GetTriageAssessment(Guid id)
    {
        var result = await _nurseService.GetTriageAssessmentAsync(id);
        if (result == null)
        {
            return NotFound(new { message = "No triage assessment found for this token." });
        }
        return Ok(result);
    }

    #endregion

    #region 3. Dashboard KPIs & Live Doctor Queues

    /// <summary>
    /// Aggregates today's clinic metrics and doctor live queue roster (GET /api/nurse/dashboard/stats).
    /// </summary>
    [HttpGet("dashboard/stats")]
    public async Task<IActionResult> GetDashboardStats()
    {
        Guid? stationId = null;
        if (User.IsInRole("Nurse"))
        {
            var stationClaim = User.FindFirst("nursingStationId")?.Value;
            if (Guid.TryParse(stationClaim, out var parsedStationId))
            {
                stationId = parsedStationId;
            }
        }

        var result = await _nurseService.GetDashboardStatsAsync(stationId);
        return Ok(result);
    }

    /// <summary>
    /// Detailed live queue for a specific doctor with priority scores and full triage vitals (GET /api/nurse/doctors/{doctorId}/queue).
    /// </summary>
    [HttpGet("doctors/{doctorId:guid}/queue")]
    public async Task<IActionResult> GetDoctorQueue(Guid doctorId)
    {
        try
        {
            var result = await _nurseService.GetDoctorQueueAsync(doctorId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Retrieves active booked tokens today for a patient selected in search (GET /api/nurse/patients/{patientId}/tokens/today).
    /// </summary>
    [HttpGet("patients/{patientId:guid}/tokens/today")]
    public async Task<IActionResult> GetPatientActiveTokens(Guid patientId)
    {
        var result = await _nurseService.GetPatientActiveTokensAsync(patientId);
        return Ok(result);
    }

    /// <summary>
    /// Complete patient profile and historical visits for the Full Details modal (GET /api/nurse/patients/{patientId}/details).
    /// </summary>
    [HttpGet("patients/{patientId:guid}/details")]
    public async Task<IActionResult> GetPatientDetails(Guid patientId)
    {
        try
        {
            var result = await _nurseService.GetPatientDetailsAsync(patientId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Computes real-time line position, wait estimates, and doctor queue visualizer for a token (GET /api/nurse/tokens/{tokenId}/queue-position).
    /// </summary>
    [HttpGet("tokens/{tokenId:guid}/queue-position")]
    public async Task<IActionResult> GetPatientQueuePosition(Guid tokenId)
    {
        try
        {
            var result = await _nurseService.GetPatientQueuePositionAsync(tokenId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    #endregion

    #region 4. Emergency Walk-In & Fast-Track Intake

    /// <summary>
    /// Enhanced walk-in intake with automatic patient deduplication and Code-Red bypass option (POST /api/nurse/tokens/walk-in-emergency).
    /// </summary>
    [HttpPost("tokens/walk-in-emergency")]
    public async Task<IActionResult> ProcessEmergencyWalkIn([FromBody] EmergencyWalkInBookingRequest request)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Guid.TryParse(userIdStr, out var nurseUserId);

        Guid? stationId = null;
        if (User.IsInRole("Nurse"))
        {
            var stationClaim = User.FindFirst("nursingStationId")?.Value;
            if (Guid.TryParse(stationClaim, out var parsedStationId))
            {
                stationId = parsedStationId;
            }
        }

        try
        {
            var token = await _nurseService.ProcessEmergencyWalkInAsync(request, nurseUserId, stationId);
            await _notificationService.NotifyTokenBookedAsync(token);

            if (request.IsEmergencyCodeRed)
            {
                await _notificationService.NotifyTriageCompletedAsync(token);
            }

            return Ok(token);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
        }
    }

    /// <summary>
    /// Issues a token for an existing registered patient with optional Code-Red emergency bypass (POST /api/nurse/patients/{patientId}/book-token).
    /// </summary>
    [HttpPost("patients/{patientId:guid}/book-token")]
    public async Task<IActionResult> BookTokenForExistingPatient(Guid patientId, [FromBody] ExistingPatientTokenBookingRequest request)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Guid.TryParse(userIdStr, out var nurseUserId);

        Guid? stationId = null;
        if (User.IsInRole("Nurse"))
        {
            var stationClaim = User.FindFirst("nursingStationId")?.Value;
            if (Guid.TryParse(stationClaim, out var parsedStationId))
            {
                stationId = parsedStationId;
            }
        }

        try
        {
            var token = await _nurseService.BookTokenForExistingPatientAsync(patientId, request, nurseUserId, stationId);
            await _notificationService.NotifyTokenBookedAsync(token);

            if (request.IsEmergencyCodeRed)
            {
                await _notificationService.NotifyTriageCompletedAsync(token);
            }

            return Ok(token);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
        }
    }

    /// <summary>
    /// Restores a skipped patient back into the doctor's active waiting queue from the Nurse Desk (POST /api/nurse/tokens/{id}/requeue).
    /// </summary>
    [HttpPost("tokens/{id:guid}/requeue")]
    public async Task<IActionResult> RequeuePatient(Guid id)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Guid.TryParse(userIdStr, out var nurseUserId);

        try
        {
            var token = await _doctorService.RequeuePatientAsync(id, nurseUserId);
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
}

