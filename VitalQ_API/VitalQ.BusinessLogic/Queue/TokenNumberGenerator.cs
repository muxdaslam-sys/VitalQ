namespace VitalQ.BusinessLogic.Services;

/// <summary>
/// Token number formatter (§08 Token number generation, Page 8).
/// Formats department code and atomic sequence number into token string (e.g. "CARD-101").
/// </summary>
public static class TokenNumberGenerator
{
    public static string Format(string departmentCode, int sequenceNumber)
    {
        return $"{departmentCode.Trim().ToUpper()}-{sequenceNumber}";
    }
}
