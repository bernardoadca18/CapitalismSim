using CapitalismSim.Simulation.Core.Economics;

namespace CapitalismSim.Simulation.Core.Population;

/// <summary>
/// Defines the basic needs (food, shelter, etc.) for a household or population cohort.
/// Each need is expressed as a daily monetary amount.
/// </summary>
/// <remarks>
/// Phase 2 uses daily cadence for finer-grained simulation. Future phases will add:
/// - More need categories (healthcare, education, transportation)
/// - Quality tiers (subsistence vs. comfortable vs. luxury)
/// - Dynamic adjustment based on economic conditions
///
/// All amounts are in minor units (e.g., cents) per day.
/// </remarks>
public readonly struct BasicNeedsProfile : IEquatable<BasicNeedsProfile>
{
    /// <summary>Daily cost of essential needs per person (food, healthcare, etc.).</summary>
    public Money DailyEssentialCostPerMember { get; }

    /// <summary>Daily cost of housing (rent or mortgage equivalent).</summary>
    public Money DailyHousingCost { get; }

    /// <summary>Daily cost of utilities (electricity, water, etc.).</summary>
    public Money DailyUtilitiesCost { get; }

    public BasicNeedsProfile(
        Money dailyEssentialCostPerMember,
        Money dailyHousingCost = default,
        Money dailyUtilitiesCost = default)
    {
        DailyEssentialCostPerMember = dailyEssentialCostPerMember;
        DailyHousingCost = dailyHousingCost;
        DailyUtilitiesCost = dailyUtilitiesCost;
    }

    /// <summary>
    /// Creates a profile with zero costs (subsistence level placeholder).
    /// </summary>
    public static BasicNeedsProfile Zero(CurrencyCode currency) => new(
        Money.Zero(currency),
        Money.Zero(currency),
        Money.Zero(currency));

    /// <summary>
    /// Calculates total daily needs for a given number of people.
    /// </summary>
    public Money CalculateTotalDaily(int memberCount)
    {
        if (memberCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(memberCount), memberCount, "Member count cannot be negative.");
        }

        return DailyEssentialCostPerMember * memberCount
            + DailyHousingCost
            + DailyUtilitiesCost;
    }

    /// <summary>
    /// Creates a new profile with updated essential cost per member.
    /// </summary>
    public BasicNeedsProfile WithDailyEssentialCostPerMember(Money amount) =>
        new(amount, DailyHousingCost, DailyUtilitiesCost);

    /// <summary>
    /// Creates a new profile with updated housing cost.
    /// </summary>
    public BasicNeedsProfile WithDailyHousingCost(Money amount) =>
        new(DailyEssentialCostPerMember, amount, DailyUtilitiesCost);

    /// <summary>
    /// Creates a new profile with updated utilities cost.
    /// </summary>
    public BasicNeedsProfile WithDailyUtilitiesCost(Money amount) =>
        new(DailyEssentialCostPerMember, DailyHousingCost, amount);

    public bool Equals(BasicNeedsProfile other) =>
        DailyEssentialCostPerMember == other.DailyEssentialCostPerMember &&
        DailyHousingCost == other.DailyHousingCost &&
        DailyUtilitiesCost == other.DailyUtilitiesCost;

    public override bool Equals(object? obj) => obj is BasicNeedsProfile other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(
        DailyEssentialCostPerMember,
        DailyHousingCost,
        DailyUtilitiesCost);

    public static bool operator ==(BasicNeedsProfile left, BasicNeedsProfile right) => left.Equals(right);

    public static bool operator !=(BasicNeedsProfile left, BasicNeedsProfile right) => !left.Equals(right);
}
