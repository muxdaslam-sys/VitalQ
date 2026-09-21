using Microsoft.AspNetCore.Mvc;
using VitalQ.BusinessLogic.Interfaces;
using VitalQ.Entities.DTOs;

namespace VitalQ.API.Controllers;

[ApiController]
[Route("api")]
public class QueueController : ControllerBase
{
    private readonly IQueueService _queueService;

    public QueueController(IQueueService queueService)
    {
        _queueService = queueService;
    }

    /// <summary>
    /// Live priority-sorted queue for a doctor (Role: Doctor).
    /// </summary>
    [HttpGet("queue/doctor/{doctorId}")]
    public async Task<IActionResult> GetDoctorQueue(Guid doctorId)
    {
        var queue = await _queueService.GetDoctorQueueAsync(doctorId);
        return Ok(queue);
    }

    /// <summary>
    /// Picks top-priority Waiting token for this doctor, moves to Called. Guarded by RowVersion (Role: Doctor).
    /// </summary>
    [HttpPost("queue/doctor/{doctorId}/call-next")]
    public async Task<IActionResult> CallNext(Guid doctorId)
    {
        var token = await _queueService.CallNextPatientAsync(doctorId);
        if (token == null) return NotFound(new { message = "No waiting patients in queue" });
        return Ok(token);
    }

    /// <summary>
    /// Holds an absent patient; token moves to Skipped (Role: Doctor).
    /// </summary>
    [HttpPost("tokens/{id}/skip")]
    public async Task<IActionResult> SkipPatient(Guid id)
    {
        var doctorUserId = Guid.Empty;
        var token = await _queueService.SkipPatientAsync(id, doctorUserId);
        return Ok(token);
    }

    /// <summary>
    /// Closes visit, stamps CompletedAtUtc, and saves ConsultationNotes (Role: Doctor).
    /// </summary>
    [HttpPost("tokens/{id}/complete")]
    public async Task<IActionResult> CompleteConsultation(Guid id, [FromBody] CompleteConsultationRequest request)
    {
        var doctorUserId = Guid.Empty;
        var token = await _queueService.CompleteConsultationAsync(id, doctorUserId, request);
        return Ok(token);
    }
}
