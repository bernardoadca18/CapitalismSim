using CapitalismSim.Simulation.Core.Entities;

namespace CapitalismSim.Simulation.Core.Population;

/// <summary>
/// Represents aggregated population cohort for Level-of-Detail (LOD) simulation.
/// Instead of tracking individuals, cohorts track counts and aggregate properties.
/// </summary>
/// <remarks>
/// When population sizes become very large (thousands to millions), tracking every
/// individual becomes prohibitively expensive. Cohorts allow the simulation to
/// represent large populations in aggregate while maintaining deterministic behavior.
///
/// Phase 2 adds independent PopulationCohortId identity separate from HouseholdId.
/// A cohort may optionally be associated with a household, or represent a regional
/// aggregate (HouseholdId.None).
///
/// Future phases will add age brackets, income distributions, and other demographic
/// properties to cohorts.
/// </remarks>
public readonly struct PopulationCohort : IEquatable<PopulationCohort>
{
    /// <summary>
    /// Unique identifier for this cohort.
    /// </summary>
    public PopulationCohortId Id { get; }

    /// <summary>
    /// Number of individuals in this cohort.
    /// </summary>
    public long Count { get; }

    /// <summary>
    /// The household this cohort belongs to, if any.
    /// HouseholdId.None for free-floating cohorts (e.g., regional aggregates).
    /// </summary>
    public HouseholdId HouseholdId { get; }

    public PopulationCohort(PopulationCohortId id, long count, HouseholdId householdId)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "Cohort count cannot be negative.");
        }

        Id = id;
        Count = count;
        HouseholdId = householdId;
    }

    public static PopulationCohort Empty(PopulationCohortId id) => new(id, 0, HouseholdId.None);

    public bool IsEmpty => Count == 0;

    public static PopulationCohort operator +(PopulationCohort left, PopulationCohort right)
    {
        if (left.Id != right.Id)
        {
            throw new InvalidOperationException(
                $"Cannot merge cohorts with different IDs: {left.Id} vs {right.Id}.");
        }

        return new PopulationCohort(left.Id, checked(left.Count + right.Count), left.HouseholdId);
    }

    public PopulationCohort WithCount(long newCount)
    {
        return new PopulationCohort(Id, newCount, HouseholdId);
    }

    public PopulationCohort WithHouseholdId(HouseholdId householdId)
    {
        return new PopulationCohort(Id, Count, householdId);
    }

    public bool Equals(PopulationCohort other) => Id.Equals(other.Id) && Count == other.Count && HouseholdId.Equals(other.HouseholdId);

    public override bool Equals(object? obj) => obj is PopulationCohort other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Id, Count, HouseholdId);

    public static bool operator ==(PopulationCohort left, PopulationCohort right) => left.Equals(right);

    public static bool operator !=(PopulationCohort left, PopulationCohort right) => !left.Equals(other: right);

    public override string ToString() =>
        Id.IsNone ? "Cohort(none)" :
        HouseholdId.IsNone ? $"Cohort(#{Id.EntityId.Value}, count={Count})" :
        $"Cohort(#{Id.EntityId.Value}, household=#{HouseholdId.EntityId.Value}, count={Count})";
}
