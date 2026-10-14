namespace VitalQ.BusinessLogic.Common;

/// <summary>
/// Computes CTAS priority scores with dynamic wait aging (+2 pts/min).
/// Enforces a 60-minute outpatient SLA ceiling (auto-escalates Green base from 100 to 500 to prevent starvation).
/// Base Weights: Red = 1000, Yellow = 500, Green = 100.
/// </summary>
public static class PriorityScoreCalculator
{
    // CTAS Base Weights
    public const int BaseWeightRed = 1000;
    public const int BaseWeightYellow = 500;
    public const int BaseWeightGreen = 100;

    // Anti-Starvation Outpatient SLA Threshold (minutes)
    public const int GreenSlaThresholdMinutes = 60;

    // Priority Ceilings
    public const int GreenMaxCeiling = 499;   // Green yields to fresh Yellow (500) during first 60 minutes
    public const int YellowMaxCeiling = 999;  // Yellow never crosses Red (1000)

    /// <summary>
    /// Calculates dynamic priority score with 60-minute SLA auto-escalation.
    /// </summary>
    public static int CalculateScore(int baseWeight, DateTime triagedAtUtc, DateTime currentTimeUtc, int agingMultiplier = 2)
    {
        var elapsedMinutes = (int)Math.Max(0, (currentTimeUtc - triagedAtUtc).TotalMinutes);

        // Auto-escalate Green patient to Yellow priority if wait time exceeds 60-minute SLA
        int effectiveBaseWeight = baseWeight;
        if (baseWeight == BaseWeightGreen && elapsedMinutes >= GreenSlaThresholdMinutes)
        {
            effectiveBaseWeight = BaseWeightYellow;
        }

        var accruedScore = effectiveBaseWeight + (elapsedMinutes * agingMultiplier);

        if (effectiveBaseWeight == BaseWeightGreen)
        {
            return Math.Min(accruedScore, GreenMaxCeiling);
        }

        if (effectiveBaseWeight == BaseWeightYellow)
        {
            return Math.Min(accruedScore, YellowMaxCeiling);
        }

        return accruedScore;
    }

    /// <summary>
    /// Checks if a waiting token has breached the 60-minute SLA threshold.
    /// </summary>
    public static bool IsEscalated(int baseWeight, DateTime triagedAtUtc, DateTime currentTimeUtc)
    {
        var elapsedMinutes = (currentTimeUtc - triagedAtUtc).TotalMinutes;
        return baseWeight == BaseWeightGreen && elapsedMinutes >= GreenSlaThresholdMinutes;
    }
}
