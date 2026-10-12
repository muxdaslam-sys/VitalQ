namespace VitalQ.BusinessLogic.Common;

/// <summary>
/// MATHEMATICAL ENGINE: CTAS Priority Aging & Anti-Starvation Calculator
/// 
/// THE CLINICAL PROBLEM:
/// In emergency and outpatient clinics, acute/emergency patients (e.g., chest pain, difficulty breathing)
/// must be seen before routine checkups.
/// However, if routine patients are constantly overtaken by new arrivals, they might wait forever
/// (a critical flaw in pure priority queues known as "Patient Starvation").
/// 
/// THE MATHEMATICAL SOLUTION:
/// PriorityScore = BaseWeight + (ElapsedWaitingMinutes * 2)
/// 
/// CLINICAL CEILINGS (SAFETY GUARANTEES):
/// - 🔴 Red (Resuscitation / Emergency): Base Weight = 100. Uncapped score. Always calls first.
/// - 🟡 Yellow (Urgent): Base Weight = 50. Hard ceiling = 99. Can NEVER overtake a Red patient.
/// - 🟢 Green (Routine / Non-Urgent): Base Weight = 10. Hard ceiling = 49. Can NEVER overtake a Yellow patient.
/// 
/// Even if a routine (Green) patient waits for 5 hours, their score steadily climbs from 10 to 49
/// (beating newer routine patients), but is capped at 49 so they NEVER unsafely jump ahead of an urgent patient.
/// </summary>
public static class PriorityScoreCalculator
{
    // CTAS Base Weights assigned by the nurse triage assessment
    public const int BaseWeightRed = 100;
    public const int BaseWeightYellow = 50;
    public const int BaseWeightGreen = 10;

    // Safety Ceilings: Prevents lower tiers from ever jumping ahead of higher urgency tiers
    public const int GreenMaxCeiling = 49;  // Routine patient score stops at 49 (Yellow starts at 50)
    public const int YellowMaxCeiling = 99; // Urgent patient score stops at 99 (Red starts at 100)

    /// <summary>
    /// Calculates the current dynamic priority score for a waiting patient based on elapsed waiting time.
    /// </summary>
    /// <param name="baseWeight">Initial weight based on CTAS triage level (10 for Green, 50 for Yellow, 100 for Red).</param>
    /// <param name="triagedAtUtc">The exact timestamp when the nurse recorded patient vitals.</param>
    /// <param name="currentTimeUtc">Current UTC timestamp (e.g., DateTime.UtcNow).</param>
    /// <param name="agingMultiplier">Points added per elapsed minute of waiting (default: 2 points/minute).</param>
    /// <returns>The capped integer priority score used to sort the doctor's queue.</returns>
    public static int CalculateScore(int baseWeight, DateTime triagedAtUtc, DateTime currentTimeUtc, int agingMultiplier = 2)
    {
        // 1. Calculate how many minutes the patient has been waiting in the clinic since triage.
        var elapsedMinutes = (int)Math.Max(0, (currentTimeUtc - triagedAtUtc).TotalMinutes);

        // 2. Compute the accrued aging score: BaseWeight + (Minutes * 2)
        var accruedScore = baseWeight + (elapsedMinutes * agingMultiplier);

        // 3. Enforce clinical ceilings based on triage tier:
        if (baseWeight == BaseWeightGreen)
        {
            // Routine patients climb up to 49, but never reach 50
            return Math.Min(accruedScore, GreenMaxCeiling);
        }

        if (baseWeight == BaseWeightYellow)
        {
            // Urgent patients climb up to 99, but never reach 100
            return Math.Min(accruedScore, YellowMaxCeiling);
        }

        // Red (Resuscitation/Emergency) is uncapped because life-threatening cases must always be seen first
        return accruedScore;
    }
}
