using CapitalismSim.Simulation.Core.Entities;

namespace CapitalismSim.Simulation.Core.Population;

/// <summary>
/// Represents an individual person in the simulation.
/// </summary>
/// <remarks>
/// This is a minimal representation for Phase 2. Future phases will add
/// career, employment, health, and other attributes. The person belongs
/// to exactly one household (or none if IsNone is true).
/// </remarks>
public readonly struct Person : IEquatable<Person>
{
    public PersonId Id { get; }
    public HouseholdId HouseholdId { get; }

    public Person(PersonId id, HouseholdId householdId)
    {
        Id = id;
        HouseholdId = householdId;
    }

    public bool IsNone => Id.IsNone;

    public static Person None => new(PersonId.None, HouseholdId.None);

    public bool Equals(Person other) => Id.Equals(other.Id) && HouseholdId.Equals(other.HouseholdId);

    public override bool Equals(object? obj) => obj is Person other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Id, HouseholdId);

    public static bool operator ==(Person left, Person right) => left.Equals(right);

    public static bool operator !=(Person left, Person right) => !left.Equals(right);

    public override string ToString() => IsNone ? "Person(none)" : $"Person(#{Id.EntityId.Value}, Household=#{HouseholdId.EntityId.Value})";
}
