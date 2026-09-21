using Microsoft.EntityFrameworkCore;
using VitalQ.Entities.Models;

namespace VitalQ.DataAccess.Repositories;

public interface IDepartmentRepository : IRepository<Department>
{
    Task<int> IncrementAndGetLastTokenNumberAsync(Guid departmentId);
}

public class DepartmentRepository : Repository<Department>, IDepartmentRepository
{
    public DepartmentRepository(VitalQDbContext context) : base(context)
    {
    }

    /// <summary>
    /// Atomic increment-and-return in SQL Server (§08 Priority engine, Page 8).
    /// Prevents duplicate token numbers across simultaneous callers.
    /// </summary>
    public async Task<int> IncrementAndGetLastTokenNumberAsync(Guid departmentId)
    {
        var nextNumber = await _context.Database
            .SqlQuery<int>($@"UPDATE Departments 
                              SET LastTokenNumber = LastTokenNumber + 1 
                              OUTPUT INSERTED.LastTokenNumber 
                              WHERE Id = {departmentId}")
            .SingleAsync();

        return nextNumber;
    }
}
