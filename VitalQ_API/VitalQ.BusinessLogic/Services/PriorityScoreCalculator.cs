namespace VitalQ.BusinessLogic.Services;

/// <summary>
/// Priority Aging calculation engine (§07 Priority engine, Page 7).
/// Prevents routine (Green) patients from starving while maintaining clinical urgency ceilings.
/// </summary>
public static class PriorityScoreCalculator
{
    public const int BaseWeightRed = 100;
    public const int BaseWeightYellow = 50;
    public const int BaseWeightGreen = 10;

    public const int GreenMaxCeiling = 49;  // Never reaches Yellow base (50)
    public const int YellowMaxCeiling = 99; // Never reaches Red base (100)

    public static int CalculateScore(int baseWeight, DateTime triagedAtUtc, DateTime currentTimeUtc, int agingMultiplier = 2)
    {
        var elapsedMinutes = (int)Math.Max(0, (currentTimeUtc - triagedAtUtc).TotalMinutes);
        var accruedScore = baseWeight + (elapsedMinutes * agingMultiplier);

        // Apply ceilings according to triage category
        if (baseWeight == BaseWeightGreen)
        {
            return Math.Min(accruedScore, GreenMaxCeiling);
        }

        if (baseWeight == BaseWeightYellow)
        {
            return Math.Min(accruedScore, YellowMaxCeiling);
        }

        // Red is uncapped
        return accruedScore;
    }
}
