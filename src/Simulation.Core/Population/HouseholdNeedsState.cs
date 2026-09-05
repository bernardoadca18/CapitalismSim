using CapitalismSim.Simulation.Core.Economics;
using CapitalismSim.Simulation.Core.Entities;

namespace CapitalismSim.Simulation.Core.Population;

/// <summary>
/// Tracks the needs satisfaction state for a household.
/// Records daily satisfaction and tracks consecutive shortfall days.
/// </summary>
/// <remarks>
/// This is used to track quality of life and can influence:
/// - Migration decisions (future phase)
/// - Political sentiment (future phase)
/// - Health outcomes (future phase)
///
/// For Phase 2, this tracks daily satisfaction with consecutive shortfall tracking.
/// </remarks>
public sealed class HouseholdNeedsState
{
    public HouseholdId HouseholdId { get; }
    public BasicNeedsProfile NeedsProfile { get; private set; }

    /// <summary>Total daily needs for the household.</summary>
    public Money TotalNeedsDaily { get; private set; }

    /// <summary>Amount satisfied today.</summary>
    public Money SatisfiedToday { get; private set; }

    /// <summary>Number of consecutive days with shortfall.</summary>
    public int ConsecutiveShortfallDays { get; private set; }

    /// <summary>Cumulative shortfall amount over time.</summary>
    public Money CumulativeShortfall { get; private set; }

    public HouseholdNeedsState(HouseholdId householdId, BasicNeedsProfile needsProfile, int memberCount)
    {
        HouseholdId = householdId;
        NeedsProfile = needsProfile;
        TotalNeedsDaily = needsProfile.CalculateTotalDaily(memberCount);
        SatisfiedToday = Money.Zero(TotalNeedsDaily.Currency);
        ConsecutiveShortfallDays = 0;
        CumulativeShortfall = Money.Zero(TotalNeedsDaily.Currency);
    }

    /// <summary>
    /// The fraction of today's needs that were satisfied (0.0 to 1.0).
    /// </summary>
    public double SatisfactionRatio =>
        TotalNeedsDaily.IsZero ? 1.0 :
        (double)SatisfiedToday.MinorUnits / TotalNeedsDaily.MinorUnits;

    /// <summary>
    /// Whether all basic needs are being met today.
    /// </summary>
    public bool IsFullySatisfied => SatisfiedToday >= TotalNeedsDaily;

    /// <summary>
    /// Updates the needs profile and recalculates total daily needs.
    /// </summary>
    public void UpdateNeedsProfile(BasicNeedsProfile profile, int memberCount)
    {
        NeedsProfile = profile;
        TotalNeedsDaily = profile.CalculateTotalDaily(memberCount);
    }

    /// <summary>
    /// Records satisfaction for the current day. Adds to the satisfied amount.
    /// </summary>
    public void RecordSatisfaction(Money amount)
    {
        if (amount.Currency != TotalNeedsDaily.Currency)
        {
            throw new InvalidOperationException(
                $"Amount currency ({amount.Currency}) doesn't match needs currency ({TotalNeedsDaily.Currency}).");
        }

        SatisfiedToday = SatisfiedToday + amount;
    }

    /// <summary>
    /// Finalizes the day's satisfaction and updates shortfall tracking.
    /// Must be called once per day after all satisfaction is recorded.
    /// </summary>
    public void FinalizeDay()
    {
        var shortfall = CalculateShortfall();

        if (!shortfall.IsZero)
        {
            // Shortfall occurred
            checked
            {
                ConsecutiveShortfallDays++;
                CumulativeShortfall = CumulativeShortfall + shortfall;
            }
        }
        else
        {
            // Full satisfaction - reset consecutive counter
            ConsecutiveShortfallDays = 0;
        }

        // Reset daily tracking
        SatisfiedToday = Money.Zero(TotalNeedsDaily.Currency);
    }

    /// <summary>
    /// Calculates the shortfall for today (unsatisfied needs).
    /// </summary>
    public Money CalculateShortfall()
    {
        if (SatisfiedToday >= TotalNeedsDaily)
        {
            return Money.Zero(TotalNeedsDaily.Currency);
        }

        return TotalNeedsDaily - SatisfiedToday;
    }

    /// <summary>
    /// Creates a deep copy of this needs state.
    /// </summary>
    public HouseholdNeedsState Clone()
    {
        return new HouseholdNeedsState(HouseholdId, NeedsProfile, memberCount: 0)
        {
            TotalNeedsDaily = TotalNeedsDaily,
            SatisfiedToday = SatisfiedToday,
            ConsecutiveShortfallDays = ConsecutiveShortfallDays,
            CumulativeShortfall = CumulativeShortfall,
        };
    }
}
