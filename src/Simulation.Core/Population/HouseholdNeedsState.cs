using CapitalismSim.Simulation.Core.Economics;
using CapitalismSim.Simulation.Core.Entities;

namespace CapitalismSim.Simulation.Core.Population;

/// <summary>
/// Tracks the needs satisfaction state for a household.
/// Records how much of the household's needs were met in a given period.
/// </summary>
/// <remarks>
/// This is used to track quality of life and can influence:
/// - Migration decisions (future phase)
/// - Political sentiment (future phase)
/// - Health outcomes (future phase)
///
/// For Phase 2, this is purely diagnostic/observational.
/// </remarks>
public sealed class HouseholdNeedsState
{
    public HouseholdId HouseholdId { get; }
    public BasicNeedsProfile NeedsProfile { get; private set; }
    public Money TotalNeedsMonthly { get; private set; }
    public Money SatisfiedNeedsMonthly { get; private set; }

    public HouseholdNeedsState(HouseholdId householdId, BasicNeedsProfile needsProfile, int memberCount)
    {
        HouseholdId = householdId;
        NeedsProfile = needsProfile;
        TotalNeedsMonthly = needsProfile.CalculateTotalMonthly(memberCount);
        SatisfiedNeedsMonthly = Money.Zero(TotalNeedsMonthly.Currency);
    }

    /// <summary>
    /// The fraction of needs that were satisfied (0.0 to 1.0).
    /// </summary>
    public double SatisfactionRatio =>
        TotalNeedsMonthly.IsZero ? 1.0 :
        (double)SatisfiedNeedsMonthly.MinorUnits / TotalNeedsMonthly.MinorUnits;

    /// <summary>
    /// Whether all basic needs are being met.
    /// </summary>
    public bool IsFullySatisfied => SatisfiedNeedsMonthly >= TotalNeedsMonthly;

    /// <summary>
    /// Updates the needs profile and recalculates total needs.
    /// </summary>
    public void UpdateNeedsProfile(BasicNeedsProfile profile, int memberCount)
    {
        NeedsProfile = profile;
        TotalNeedsMonthly = profile.CalculateTotalMonthly(memberCount);
    }

    /// <summary>
    /// Records satisfaction for a period. Adds to the satisfied amount.
    /// </summary>
    public void RecordSatisfaction(Money amount)
    {
        if (amount.Currency != TotalNeedsMonthly.Currency)
        {
            throw new InvalidOperationException(
                $"Amount currency ({amount.Currency}) doesn't match needs currency ({TotalNeedsMonthly.Currency}).");
        }

        SatisfiedNeedsMonthly = SatisfiedNeedsMonthly + amount;
    }

    /// <summary>
    /// Resets the satisfied amount for a new period.
    /// </summary>
    public void ResetForNewPeriod()
    {
        SatisfiedNeedsMonthly = Money.Zero(TotalNeedsMonthly.Currency);
    }

    /// <summary>
    /// Calculates the shortfall (unsatisfied needs).
    /// </summary>
    public Money CalculateShortfall()
    {
        if (SatisfiedNeedsMonthly >= TotalNeedsMonthly)
        {
            return Money.Zero(TotalNeedsMonthly.Currency);
        }

        return TotalNeedsMonthly - SatisfiedNeedsMonthly;
    }
}
