using CapitalismSim.Simulation.Core.Entities;

namespace CapitalismSim.Simulation.Core.Population;

/// <summary>
/// Represents an aggregated population cohort for Level-of-Detail (LOD) simulation.
/// Instead of tracking individuals, cohorts track counts and aggregate properties.
/// </summary>
/// <remarks>
/// When population sizes become very large (thousands to millions), tracking every
/// individual becomes prohibitively expensive. Cohorts allow the simulation to
/// represent large populations in aggregate while maintaining deterministic behavior.
///
/// Future phases will add age brackets, income distributions, and other demographic
/// properties to cohorts.
/// </remarks>
public readonly struct PopulationCohort : IEquatable<PopulationCohort>
{
    /// <summary>
    /// Number of individuals in this cohort.
    /// </summary>
    public long Count { get; }

    /// <summary>
    /// The household this cohort belongs to (if cohort represents household members).
    /// HouseholdId.None for free-floating cohorts (e.g., regional aggregates).
    /// </summary>
    public HouseholdId HouseholdId { get; }

    public PopulationCohort(long count, HouseholdId householdId)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "Cohort count cannot be negative.");
        }

        Count = count;
        HouseholdId = householdId;
    }

    public static PopulationCohort Empty(HouseholdId householdId) => new(0, householdId);

    public bool IsEmpty => Count == 0;

    public static PopulationCohort operator +(PopulationCohort left, PopulationCohort right)
    {
        if (left.HouseholdId != right.HouseholdId)
        {
            throw new InvalidOperationException(
                $"Cannot merge cohorts from different households: {left.HouseholdId} and {right.HouseholdId}.");
        }

        return new PopulationCohort(checked(left.Count + right.Count), left.HouseholdId);
    }

    public PopulationCohort WithCount(long newCount)
    {
        return new PopulationCohort(newCount, HouseholdId);
    }

    public bool Equals(PopulationCohort other) => Count == other.Count && HouseholdId.Equals(other.HouseholdId);

    public override bool Equals(object? obj) => obj is PopulationCohort other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Count, HouseholdId);

    public static bool operator ==(PopulationCohort left, PopulationCohort right) => left.Equals(right);

    public static bool operator !=(PopulationCohort left, PopulationCohort right) => !left.Equals(right);

    public override string ToString()
    {
        return HouseholdId.IsNone
            ? $"Cohort({Count} individuals, unattached)"
            : $"Cohort({Count} individuals, Household=#{HouseholdId.EntityId.Value})";
    }
}
