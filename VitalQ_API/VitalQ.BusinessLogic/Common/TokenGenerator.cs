using System.Data;
using Microsoft.EntityFrameworkCore;
using VitalQ.DataAccess;

namespace VitalQ.BusinessLogic.Common;

/// <summary>
/// Generates atomic, daily-resetting sequence tokens per department via SQL Server row-level locking.
/// Format: DEPT-YYMMDD-NNN (e.g. CARD-261012-001).
/// </summary>
public static class TokenGenerator
{
    /// <summary>
    /// Atomically increments and formats the next sequential token number for a clinical department.
    /// </summary>
    public static async Task<string> GenerateAsync(VitalQDbContext context, Guid departmentId, string departmentCode)
    {
        await using var cmd = context.Database.GetDbConnection().CreateCommand();
        
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
        if (scalarResult == null || scalarResult == DBNull.Value)
        {
            throw new KeyNotFoundException($"Clinical department with ID '{departmentId}' was not found.");
        }

        var nextNumber = Convert.ToInt32(scalarResult);
        var dateStr = DateTime.UtcNow.ToString("yyMMdd");
        var code = string.IsNullOrWhiteSpace(departmentCode) ? "GEN" : departmentCode.Trim().ToUpper();

        return $"{code}-{dateStr}-{nextNumber:D3}";
    }
}
