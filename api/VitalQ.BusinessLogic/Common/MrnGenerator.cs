using System.Security.Cryptography;

namespace VitalQ.BusinessLogic.Common;

/// <summary>
/// Generates unique, collision-resistant Medical Record Numbers (MRNs) using UTC date prefix and cryptographic hex bytes.
/// Format: MRN-YYMMDD-XXXXXXXX (e.g. MRN-261103-7A3F9B2C).
/// </summary>
public static class MrnGenerator
{
    /// <summary>
    /// Generates a human-friendly, collision-safe MRN string (19 characters, well under 30-char database limit).
    /// Uses 4 bytes (32-bit entropy: 4.29 billion combinations/day).
    /// </summary>
    public static string Generate()
    {
        var dateStr = DateTime.UtcNow.ToString("yyMMdd");

        var buffer = new byte[4];
        RandomNumberGenerator.Fill(buffer);
        var hexCode = Convert.ToHexString(buffer);

        return $"MRN-{dateStr}-{hexCode}";
    }
}
