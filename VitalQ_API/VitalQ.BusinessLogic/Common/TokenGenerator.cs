using System.Data;
using Microsoft.EntityFrameworkCore;
using VitalQ.BusinessLogic.Interfaces;
using VitalQ.DataAccess;

namespace VitalQ.BusinessLogic.Services;

/// <summary>
/// Executes atomic SQL update on the department row to generate sequential token numbers.
/// Resets every day at 00:00 UTC. Thread-safe and lock-free at the application layer.
/// </summary>
public class TokenGenerator : ITokenGenerator
{
    private readonly VitalQDbContext _context;

    public TokenGenerator(VitalQDbContext context)
    {
        _context = context;
    }

    public async Task<string> GenerateTokenNumberAsync(Guid departmentId, string departmentCode)
    {
        await using var cmd = _context.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = @"
            UPDATE Departments 
            SET LastTokenNumber = CASE 
                    WHEN LastTokenDate = CAST(GETUTCDATE() AS DATE) THEN LastTokenNumber + 1 
                    ELSE 1 
                END,
                LastTokenDate = CAST(GETUTCDATE() AS DATE)
            OUTPUT INSERTED.LastTokenNumber 
            WHERE Id = @deptId";

        var param = cmd.CreateParameter();
        param.ParameterName = "@deptId";
        param.Value = departmentId;
        cmd.Parameters.Add(param);

        if (cmd.Connection!.State != ConnectionState.Open)
        {
            await cmd.Connection.OpenAsync();
        }

        var scalarResult = await cmd.ExecuteScalarAsync();
        var nextNumber = Convert.ToInt32(scalarResult);

        // Format: DEPT-YYMMDD-NNN (e.g. CARD-261005-001) - 15 chars, permanent multi-year uniqueness
        var dateStr = DateTime.UtcNow.ToString("yyMMdd");
        var code = string.IsNullOrWhiteSpace(departmentCode) ? "GEN" : departmentCode.Trim().ToUpper();

        return $"{code}-{dateStr}-{nextNumber:D3}";
    }
}
