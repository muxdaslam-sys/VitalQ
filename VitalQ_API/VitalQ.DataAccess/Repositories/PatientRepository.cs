using Microsoft.EntityFrameworkCore;
using VitalQ.Entities.Models;

namespace VitalQ.DataAccess.Repositories;

public interface IPatientRepository : IRepository<Patient>
{
    Task<Patient?> GetByUserIdAsync(Guid userId);
    Task<Patient?> GetByPhoneNumberAsync(string phoneNumber);
    Task<List<Patient>> SearchAsync(string query);
}

public class PatientRepository : Repository<Patient>, IPatientRepository
{
    public PatientRepository(VitalQDbContext context) : base(context)
    {
    }

    public async Task<Patient?> GetByUserIdAsync(Guid userId)
    {
        return await _context.Patients
            .FirstOrDefaultAsync(p => p.UserId == userId);
    }

    public async Task<Patient?> GetByPhoneNumberAsync(string phoneNumber)
    {
        return await _context.Patients
            .FirstOrDefaultAsync(p => p.PhoneNumber == phoneNumber);
    }

    public async Task<List<Patient>> SearchAsync(string query)
    {
        return await _context.Patients
            .Include(p => p.QueueTokens)
            .Where(p => p.PhoneNumber.Contains(query) || 
                        p.FullName.Contains(query) || 
                        p.MedicalRecordNumber.Contains(query) ||
                        p.QueueTokens.Any(t => t.TokenNumber.Contains(query)))
            .ToListAsync();
    }
}
