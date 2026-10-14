namespace VitalQ.BusinessLogic.Common;

/// <summary>
/// MATHEMATICAL ENGINE: CTAS Priority Aging & 60-Minute Anti-Starvation SLA Calculator
/// 
/// THE CLINICAL PROBLEM:
/// If a continuous influx of urgent (Yellow) patients arrives, routine (Green) patients
/// could sit in the clinic for hours without ever being called (a fatal queue defect known as "Starvation").
/// 
/// THE 60-MINUTE OUTPATIENT SLA SOLUTION:
/// - 🔴 Red (Resuscitation / Immediate Emergency): Base = 1000. Uncapped. Always called first.
/// - 🟡 Yellow (Urgent): Base = 500. Hard ceiling = 999. Can NEVER overtake Red.
/// - 🟢 Green (Routine / Non-Urgent): Base = 100.
///   • First 60 Minutes: Capped at 499 (yields safely to genuinely urgent Yellow cases).
///   • AFTER 60 MINUTES (SLA BREACH): Effective base weight auto-escalates to Yellow (500)!
///     Because the Green patient arrived 60+ minutes earlier, their earlier timestamp
///     causes them to leap ahead of newly arrived Yellow patients!
/// </summary>
public static class PriorityScoreCalculator
{
    // CTAS Base Weights (Synchronized 1:1 with NurseService)
    public const int BaseWeightRed = 1000;
    public const int BaseWeightYellow = 500;
    public const int BaseWeightGreen = 100;

    // Outpatient Anti-Starvation SLA Threshold (in minutes)
    public const int GreenSlaThresholdMinutes = 60; // 1-hour maximum wait guarantee

    // Clinical Ceilings: Prevents tiers from crossing unless SLA is breached
    public const int GreenMaxCeiling = 499;   // Normal Green stops at 499 (Yellow starts at 500)
    public const int YellowMaxCeiling = 999;  // Yellow stops at 999 (Red starts at 1000)

    /// <summary>
    /// Calculates the dynamic priority score with 60-Minute Anti-Starvation SLA escalation.
    /// </summary>
    /// <param name="baseWeight">Initial weight based on CTAS triage level (100 for Green, 500 for Yellow, 1000 for Red).</param>
    /// <param name="triagedAtUtc">The exact timestamp when the nurse recorded patient vitals.</param>
    /// <param name="currentTimeUtc">Current UTC timestamp.</param>
    /// <param name="agingMultiplier">Points added per elapsed minute of waiting (default: 2 points/minute).</param>
    /// <returns>The calculated priority score used to rank patients in the doctor's queue.</returns>
    public static int CalculateScore(int baseWeight, DateTime triagedAtUtc, DateTime currentTimeUtc, int agingMultiplier = 2)
    {
        // 1. Calculate how many minutes the patient has been waiting in the clinic since vitals triage
        var elapsedMinutes = (int)Math.Max(0, (currentTimeUtc - triagedAtUtc).TotalMinutes);

        // 2. Anti-Starvation SLA Escalation:
        // If a Green patient has waited >= 60 minutes, automatically escalate effective base weight to Yellow (500)
        int effectiveBaseWeight = baseWeight;
        if (baseWeight == BaseWeightGreen && elapsedMinutes >= GreenSlaThresholdMinutes)
        {
            effectiveBaseWeight = BaseWeightYellow; // Promoted to 500!
        }

        // 3. Compute accrued score: Base + (Minutes * Multiplier)
        var accruedScore = effectiveBaseWeight + (elapsedMinutes * agingMultiplier);

        // 4. Enforce clinical ceilings
        if (effectiveBaseWeight == BaseWeightGreen)
        {
            // Routine patient within first 60 minutes climbs up to 499, but yields to fresh Yellow (500)
            return Math.Min(accruedScore, GreenMaxCeiling);
        }

        if (effectiveBaseWeight == BaseWeightYellow)
        {
            // Urgent patient (or SLA-escalated routine patient) climbs up to 999, but never reaches Red (1000)
            return Math.Min(accruedScore, YellowMaxCeiling);
        }

        // Red (Resuscitation) is uncapped because life-threatening cases must always be seen immediately
        return accruedScore;
    }

    /// <summary>
    /// Helper to detect if a waiting token has breached the 60-minute SLA and is currently auto-escalated.
    /// </summary>
    public static bool IsEscalated(int baseWeight, DateTime triagedAtUtc, DateTime currentTimeUtc)
    {
        var elapsedMinutes = (currentTimeUtc - triagedAtUtc).TotalMinutes;
        return baseWeight == BaseWeightGreen && elapsedMinutes >= GreenSlaThresholdMinutes;
    }
}
