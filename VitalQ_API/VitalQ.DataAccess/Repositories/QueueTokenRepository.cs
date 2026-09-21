using Microsoft.EntityFrameworkCore;
using VitalQ.Entities.Models;

namespace VitalQ.DataAccess.Repositories;

public interface IQueueTokenRepository : IRepository<QueueToken>
{
    Task<QueueToken?> GetTokenWithDetailsAsync(Guid tokenId);
    Task<List<QueueToken>> GetWaitingTokensByDoctorAsync(Guid doctorId);
    Task<List<QueueToken>> GetWaitingTokensByDepartmentAsync(Guid departmentId);
    Task<QueueToken?> GetTopPriorityWaitingTokenAsync(Guid doctorId);
    Task<QueueToken?> GetActiveTokenByPatientIdAsync(Guid patientId);
}

public class QueueTokenRepository : Repository<QueueToken>, IQueueTokenRepository
{
    public QueueTokenRepository(VitalQDbContext context) : base(context)
    {
    }

    public async Task<QueueToken?> GetTokenWithDetailsAsync(Guid tokenId)
    {
        return await _context.QueueTokens
            .Include(t => t.Patient)
            .Include(t => t.Department)
            .Include(t => t.Doctor)
            .Include(t => t.TriageAssessment)
            .FirstOrDefaultAsync(t => t.Id == tokenId);
    }

    public async Task<List<QueueToken>> GetWaitingTokensByDoctorAsync(Guid doctorId)
    {
        return await _context.QueueTokens
            .Include(t => t.Patient)
            .Include(t => t.TriageAssessment)
            .Where(t => t.DoctorId == doctorId && t.Status == "Waiting")
            .OrderByDescending(t => t.PriorityScore)
            .ThenBy(t => t.BookedAtUtc)
            .ToListAsync();
    }

    public async Task<List<QueueToken>> GetWaitingTokensByDepartmentAsync(Guid departmentId)
    {
        return await _context.QueueTokens
            .Include(t => t.Patient)
            .Include(t => t.Doctor)
            .Include(t => t.TriageAssessment)
            .Where(t => t.DepartmentId == departmentId && t.Status == "Waiting")
            .OrderByDescending(t => t.PriorityScore)
            .ThenBy(t => t.BookedAtUtc)
            .ToListAsync();
    }

    public async Task<QueueToken?> GetTopPriorityWaitingTokenAsync(Guid doctorId)
    {
        return await _context.QueueTokens
            .Include(t => t.Patient)
            .Include(t => t.Department)
            .Where(t => t.DoctorId == doctorId && t.Status == "Waiting")
            .OrderByDescending(t => t.PriorityScore)
            .ThenBy(t => t.BookedAtUtc)
            .FirstOrDefaultAsync();
    }

    public async Task<QueueToken?> GetActiveTokenByPatientIdAsync(Guid patientId)
    {
        return await _context.QueueTokens
            .Include(t => t.Department)
            .Include(t => t.Doctor)
            .Include(t => t.TriageAssessment)
            .Where(t => t.PatientId == patientId && t.Status != "Completed" && t.Status != "Cancelled")
            .OrderByDescending(t => t.BookedAtUtc)
            .FirstOrDefaultAsync();
    }
}
