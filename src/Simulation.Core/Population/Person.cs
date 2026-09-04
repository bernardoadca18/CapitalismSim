using CapitalismSim.Simulation.Core.Entities;

namespace CapitalismSim.Simulation.Core.Population;

/// <summary>
/// Represents an individual person in the simulation.
/// </summary>
/// <remarks>
/// This is a minimal representation for Phase 2. Future phases will add
/// career, employment, health, and other attributes. The person belongs
/// to exactly one household (or none if IsNone is true).
///
/// Phase 2 adds:
/// - Age for minimal demographics. Age is tracked in simulation years (integer)
///   and can be incremented by scheduled systems.
/// - Personal account for individual finances. This is separate from the
///   household's shared account. Not all persons have personal accounts
///   (AccountId.None indicates no personal account).
/// </remarks>
public readonly struct Person : IEquatable<Person>
{
    public PersonId Id { get; }
    public HouseholdId HouseholdId { get; }

    /// <summary>
    /// The person's age in simulation years. Non-negative.
    /// </summary>
    public int Age { get; }

    /// <summary>
    /// The person's personal monetary account, if any.
    /// AccountId.None means the person has no personal account.
    /// </summary>
    public AccountId PersonalAccountId { get; }

    public Person(PersonId id, HouseholdId householdId, int age = 0, AccountId personalAccountId = default)
    {
        if (age < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(age), age, "Age cannot be negative.");
        }

        Id = id;
        HouseholdId = householdId;
        Age = age;
        PersonalAccountId = personalAccountId;
    }

    public bool IsNone => Id.IsNone;

    public static Person None => new(PersonId.None, HouseholdId.None, 0);

    /// <summary>
    /// Creates a new Person with an incremented age.
    /// </summary>
    public Person WithIncrementedAge()
    {
        return new Person(Id, HouseholdId, checked(Age + 1), PersonalAccountId);
    }

    /// <summary>
    /// Creates a new Person with a different household.
    /// </summary>
    public Person WithHouseholdId(HouseholdId householdId)
    {
        return new Person(Id, householdId, Age, PersonalAccountId);
    }

    /// <summary>
    /// Creates a new Person with a personal account.
    /// </summary>
    public Person WithPersonalAccountId(AccountId personalAccountId)
    {
        return new Person(Id, HouseholdId, Age, personalAccountId);
    }

    public bool Equals(Person other) =>
        Id.Equals(other.Id) &&
        HouseholdId.Equals(other.HouseholdId) &&
        Age == other.Age &&
        PersonalAccountId.Equals(other.PersonalAccountId);

    public override bool Equals(object? obj) => obj is Person other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Id, HouseholdId, Age, PersonalAccountId);

    public static bool operator ==(Person left, Person right) => left.Equals(right);

    public static bool operator !=(Person left, Person right) => !left.Equals(right);

    public override string ToString() =>
        IsNone ? "Person(none)" :
        PersonalAccountId.IsNone ? $"Person(#{Id.EntityId.Value}, age={Age}, household=#{HouseholdId.EntityId.Value})" :
        $"Person(#{Id.EntityId.Value}, age={Age}, household=#{HouseholdId.EntityId.Value}, account=#{PersonalAccountId.EntityId.Value})";
}
