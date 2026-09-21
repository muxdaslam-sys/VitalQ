using Microsoft.EntityFrameworkCore;
using VitalQ.Entities.Models;

namespace VitalQ.DataAccess.Repositories;

public interface IDoctorRepository : IRepository<Doctor>
{
    Task<List<Doctor>> GetAvailableByDepartmentAsync(Guid departmentId);
    Task<Doctor?> GetDoctorWithDetailsAsync(Guid doctorId);
}

public class DoctorRepository : Repository<Doctor>, IDoctorRepository
{
    public DoctorRepository(VitalQDbContext context) : base(context)
    {
    }

    public async Task<List<Doctor>> GetAvailableByDepartmentAsync(Guid departmentId)
    {
        return await _context.Doctors
            .Include(d => d.User)
            .Where(d => d.DepartmentId == departmentId && d.Status == "Available")
            .ToListAsync();
    }

    public async Task<Doctor?> GetDoctorWithDetailsAsync(Guid doctorId)
    {
        return await _context.Doctors
            .Include(d => d.User)
            .Include(d => d.Department)
            .FirstOrDefaultAsync(d => d.Id == doctorId);
    }
}
