using System.Security.Cryptography;

namespace VitalQ.BusinessLogic.Common;

/// <summary>
/// Generates unique 15-character Medical Record Numbers (MRNs) using UTC date prefix and cryptographic hex bytes.
/// Format: MRN-YYMMDD-XXXX (e.g. MRN-261012-7A3F).
/// </summary>
public static class MrnGenerator
{
    /// <summary>
    /// Generates a human-friendly 15-character MRN string.
    /// </summary>
    public static string Generate()
    {
        var dateStr = DateTime.UtcNow.ToString("yyMMdd");

        var buffer = new byte[2];
        RandomNumberGenerator.Fill(buffer);
        var hexCode = Convert.ToHexString(buffer);

        return $"MRN-{dateStr}-{hexCode}";
    }
}
