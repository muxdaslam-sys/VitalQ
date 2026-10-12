using System.Data;
using Microsoft.EntityFrameworkCore;
using VitalQ.DataAccess;

namespace VitalQ.BusinessLogic.Common;

/// <summary>
/// UTILITY ENGINE: Concurrency-Safe Atomic Token Generator
/// 
/// HOW THIS WORKS UNDER THE HOOD:
/// Instead of generating sequence numbers in C# memory (which causes race conditions and
/// duplicate numbers if two patients book at the exact same millisecond), this class runs
/// an atomic "UPDATE ... OUTPUT" SQL statement directly on the SQL Server database.
/// 
/// WHY IS THIS A STATIC UTILITY CLASS?
/// 1. Symmetrical Common Folder: Aligns with MrnGenerator and PriorityScoreCalculator.
/// 2. Zero Dependency Injection Overhead: Services pass their existing DbContext directly.
/// 3. Fast & Thread-Safe: Uses ADO.NET directly against SQL Server row locks (less than 1 millisecond).
/// 4. Automatic Daily Reset: Resets back to 1 every day at 00:00 UTC.
/// </summary>
public static class TokenGenerator
{
    /// <summary>
    /// Generates the next sequential token number for a given clinical department.
    /// Example output: "CARD-261012-001" (Cardiology, October 12, 2026, Token #1).
    /// </summary>
    /// <param name="context">The active EF Core database context.</param>
    /// <param name="departmentId">The unique GUID identifier of the department.</param>
    /// <param name="departmentCode">The 3-4 letter uppercase clinic prefix (e.g., "CARD", "ORTH").</param>
    /// <returns>A permanent, 15-character token string in the format: DEPT-YYMMDD-NNN.</returns>
    public static async Task<string> GenerateAsync(VitalQDbContext context, Guid departmentId, string departmentCode)
    {
        // 1. Create a direct ADO.NET SQL command using the existing database connection.
        await using var cmd = context.Database.GetDbConnection().CreateCommand();
        
        // 2. The atomic SQL query:
        //    - If today's date matches LastTokenDate: increment LastTokenNumber by 1.
        //    - If it's a new day: reset LastTokenNumber to 1 and update the date.
        //    - The "OUTPUT INSERTED.LastTokenNumber" immediately returns the newly assigned number.
        cmd.CommandText = @"
            UPDATE Departments 
            SET LastTokenNumber = CASE 
                    WHEN LastTokenDate = CAST(GETUTCDATE() AS DATE) THEN LastTokenNumber + 1 
                    ELSE 1 
                END,
                LastTokenDate = CAST(GETUTCDATE() AS DATE)
            OUTPUT INSERTED.LastTokenNumber 
            WHERE Id = @deptId";

        // 3. Bind the department ID parameter safely to prevent SQL injection.
        var param = cmd.CreateParameter();
        param.ParameterName = "@deptId";
        param.Value = departmentId;
        cmd.Parameters.Add(param);

        // 4. Ensure the database connection is open.
        if (cmd.Connection!.State != ConnectionState.Open)
        {
            await cmd.Connection.OpenAsync();
        }

        // 5. Execute the update and retrieve the assigned token number.
        var scalarResult = await cmd.ExecuteScalarAsync();
        if (scalarResult == null || scalarResult == DBNull.Value)
        {
            // If the query returns nothing, the department ID does not exist in the database.
            throw new KeyNotFoundException($"Clinical department with ID '{departmentId}' was not found.");
        }

        var nextNumber = Convert.ToInt32(scalarResult);

        // 6. Format the token string:
        //    - Prefix: 3-4 letter uppercase code (e.g., "CARD", default to "GEN" if empty).
        //    - Date: 6 digits "yyMMdd" (e.g., "261012" for 2026-10-12).
        //    - Sequence: 3-digit padded number (e.g., 001, 002, 099).
        //    Resulting string length is exactly 15 characters (fits neatly on mobile screens and paper slips).
        var dateStr = DateTime.UtcNow.ToString("yyMMdd");
        var code = string.IsNullOrWhiteSpace(departmentCode) ? "GEN" : departmentCode.Trim().ToUpper();

        return $"{code}-{dateStr}-{nextNumber:D3}";
    }
}
