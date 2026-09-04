using CapitalismSim.Simulation.Core.Economics;

namespace CapitalismSim.Simulation.Core.Population;

/// <summary>
/// Defines the basic needs (food, shelter, etc.) for a household or population cohort.
/// Each need is expressed as a monetary amount per time period.
/// </summary>
/// <remarks>
/// This is a minimal representation for Phase 2. Future phases will add:
/// - More need categories (healthcare, education, transportation)
/// - Quality tiers (subsistence vs. comfortable vs. luxury)
/// - Dynamic adjustment based on economic conditions
///
/// All amounts are in minor units (e.g., cents) per the specified period.
/// The period is typically one month for household budgeting.
/// </remarks>
public readonly struct BasicNeedsProfile : IEquatable<BasicNeedsProfile>
{
    /// <summary>Monthly cost of food per person.</summary>
    public Money FoodPerPersonMonthly { get; }

    /// <summary>Monthly cost of housing (rent or mortgage equivalent).</summary>
    public Money HousingMonthly { get; }

    /// <summary>Monthly cost of utilities (electricity, water, etc.).</summary>
    public Money UtilitiesMonthly { get; }

    /// <summary>Monthly cost of healthcare per person.</summary>
    public Money HealthcarePerPersonMonthly { get; }

    public BasicNeedsProfile(
        Money foodPerPersonMonthly,
        Money housingMonthly,
        Money utilitiesMonthly,
        Money healthcarePerPersonMonthly)
    {
        FoodPerPersonMonthly = foodPerPersonMonthly;
        HousingMonthly = housingMonthly;
        UtilitiesMonthly = utilitiesMonthly;
        HealthcarePerPersonMonthly = healthcarePerPersonMonthly;
    }

    /// <summary>
    /// Creates a profile with zero costs (subsistence level placeholder).
    /// </summary>
    public static BasicNeedsProfile Zero(CurrencyCode currency) => new(
        Money.Zero(currency),
        Money.Zero(currency),
        Money.Zero(currency),
        Money.Zero(currency));

    /// <summary>
    /// Calculates total monthly needs for a given number of people.
    /// </summary>
    public Money CalculateTotalMonthly(int personCount)
    {
        if (personCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(personCount), personCount, "Person count cannot be negative.");
        }

        return FoodPerPersonMonthly * personCount
            + HousingMonthly
            + UtilitiesMonthly
            + HealthcarePerPersonMonthly * personCount;
    }

    /// <summary>
    /// Creates a new profile with updated food cost per person.
    /// </summary>
    public BasicNeedsProfile WithFoodPerPersonMonthly(Money amount) =>
        new(amount, HousingMonthly, UtilitiesMonthly, HealthcarePerPersonMonthly);

    /// <summary>
    /// Creates a new profile with updated housing cost.
    /// </summary>
    public BasicNeedsProfile WithHousingMonthly(Money amount) =>
        new(FoodPerPersonMonthly, amount, UtilitiesMonthly, HealthcarePerPersonMonthly);

    /// <summary>
    /// Creates a new profile with updated utilities cost.
    /// </summary>
    public BasicNeedsProfile WithUtilitiesMonthly(Money amount) =>
        new(FoodPerPersonMonthly, HousingMonthly, amount, HealthcarePerPersonMonthly);

    /// <summary>
    /// Creates a new profile with updated healthcare cost per person.
    /// </summary>
    public BasicNeedsProfile WithHealthcarePerPersonMonthly(Money amount) =>
        new(FoodPerPersonMonthly, HousingMonthly, UtilitiesMonthly, amount);

    public bool Equals(BasicNeedsProfile other) =>
        FoodPerPersonMonthly == other.FoodPerPersonMonthly &&
        HousingMonthly == other.HousingMonthly &&
        UtilitiesMonthly == other.UtilitiesMonthly &&
        HealthcarePerPersonMonthly == other.HealthcarePerPersonMonthly;

    public override bool Equals(object? obj) => obj is BasicNeedsProfile other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(
        FoodPerPersonMonthly,
        HousingMonthly,
        UtilitiesMonthly,
        HealthcarePerPersonMonthly);

    public static bool operator ==(BasicNeedsProfile left, BasicNeedsProfile right) => left.Equals(right);

    public static bool operator !=(BasicNeedsProfile left, BasicNeedsProfile right) => !left.Equals(right);
}
