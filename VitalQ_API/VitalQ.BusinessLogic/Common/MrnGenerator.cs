using System.Security.Cryptography;

namespace VitalQ.BusinessLogic.Common;

/// <summary>
/// UTILITY: Permanent Medical Record Number (MRN) Generator
/// 
/// WHAT IS AN MRN?
/// An MRN is a patient's lifelong hospital identifier (like a patient passport).
/// Every patient has one unique MRN that links all their past visits, prescriptions,
/// diagnoses, and lab results across years of care.
/// 
/// WHY THIS DESIGN?
/// 1. Compact & Human-Friendly: Exactly 15 characters (e.g., "MRN-261012-7A3F").
///    Patients can easily read it over the phone or speak it at the clinic desk.
/// 2. Fast: Generated purely in CPU memory using cryptographic bytes (~50 nanoseconds).
///    It requires zero database lookups or locks to generate.
/// 3. Cross-Day Safety: The date prefix "YYMMDD" ensures the random 4-character hex suffix
///    resets daily, giving 65,536 unique combinations per day.
/// </summary>
public static class MrnGenerator
{
    /// <summary>
    /// Generates a human-friendly 15-character MRN string.
    /// Example output: "MRN-261012-7A3F" (Created on 2026-10-12, code 7A3F).
    /// </summary>
    public static string Generate()
    {
        // 1. Current UTC date in 6-digit format: "YYMMDD"
        var dateStr = DateTime.UtcNow.ToString("yyMMdd");

        // 2. Generate 2 cryptographically strong random bytes (4 uppercase hex characters)
        var buffer = new byte[2];
        RandomNumberGenerator.Fill(buffer);
        var hexCode = Convert.ToHexString(buffer);

        // 3. Assemble: "MRN-" (4) + "261012" (6) + "-" (1) + "7A3F" (4) = 15 characters total
        return $"MRN-{dateStr}-{hexCode}";
    }
}
