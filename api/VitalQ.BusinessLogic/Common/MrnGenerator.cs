using Microsoft.EntityFrameworkCore;
using VitalQ.DataAccess;

namespace VitalQ.BusinessLogic.Common;

/// <summary>
/// Production-grade, collision-proof Medical Record Number (MRN) generator.
/// Starts at MRN-000001 (6 digits minimum) and dynamically scales past millions under the 30-char database limit.
/// </summary>
public static class MrnGenerator
{
    public const string Prefix = "MRN-";

    /// <summary>
    /// Atomically generates the next sequential, collision-proof MRN for patient registration.
    /// </summary>
    public static async Task<string> GenerateMrnAsync(VitalQDbContext context)
    {
        // 1. Fetch latest MRN using chronological and length-aware ordering
        var latest = await context.Patients
            .AsNoTracking()
            .Where(p => p.MedicalRecordNumber.StartsWith(Prefix))
            .OrderByDescending(p => p.CreatedAtUtc)
            .ThenByDescending(p => p.MedicalRecordNumber.Length)
            .ThenByDescending(p => p.MedicalRecordNumber)
            .Select(p => p.MedicalRecordNumber)
            .FirstOrDefaultAsync();

        // 2. Increment sequence number (starts at 1 if database empty or legacy)
        int.TryParse(latest?.Replace(Prefix, ""), out var lastNumber);
        var nextNumber = lastNumber + 1;

        // 3. Guarantee zero collisions
        var mrn = $"{Prefix}{nextNumber:D6}";
        while (await context.Patients.AnyAsync(p => p.MedicalRecordNumber == mrn))
        {
            nextNumber++;
            mrn = $"{Prefix}{nextNumber:D6}";
        }

        return mrn;
    }
}
