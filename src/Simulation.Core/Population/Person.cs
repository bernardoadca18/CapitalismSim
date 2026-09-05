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
/// Phase 2 demographics:
/// - BirthDayIndex: The simulation day the person was born, relative to epoch.
///   Can be negative (born before simulation start). Age is derived from this
///   and the current simulation day using a 365-day year approximation.
/// - Personal account for individual finances. This is separate from the
///   household's shared account. Not all persons have personal accounts
///   (AccountId.None indicates no personal account).
///
/// Year length approximation: 365 days exactly (no leap years, no calendar).
/// This keeps age calculation simple and deterministic.
/// </remarks>
public readonly struct Person : IEquatable<Person>
{
    /// <summary>
    /// Days in a simulation year. Used for age calculation.
    /// </summary>
    public const int DaysPerYear = 365;

    public PersonId Id { get; }
    public HouseholdId HouseholdId { get; }

    /// <summary>
    /// The simulation day the person was born, relative to epoch.
    /// Can be negative (born before simulation start).
    /// Age is derived via GetAge(currentSimulationDay).
    /// </summary>
    public long BirthDayIndex { get; }

    /// <summary>
    /// The person's personal monetary account, if any.
    /// AccountId.None means the person has no personal account.
    /// </summary>
    public AccountId PersonalAccountId { get; }

    /// <summary>
    /// Creates a new Person.
    /// </summary>
    /// <param name="id">Unique person identifier.</param>
    /// <param name="householdId">Household this person belongs to.</param>
    /// <param name="birthDayIndex">Simulation day of birth relative to epoch. Negative = born before epoch.</param>
    /// <param name="personalAccountId">Personal monetary account, if any.</param>
    public Person(PersonId id, HouseholdId householdId, long birthDayIndex = 0, AccountId personalAccountId = default)
    {
        Id = id;
        HouseholdId = householdId;
        BirthDayIndex = birthDayIndex;
        PersonalAccountId = personalAccountId;
    }

    public bool IsNone => Id.IsNone;

    public static Person None => new(PersonId.None, HouseholdId.None, 0);

    /// <summary>
    /// Calculates the person's age in years at the given simulation day.
    /// </summary>
    /// <param name="currentSimulationDay">The current simulation day (days since epoch).</param>
    /// <returns>Age in years (non-negative).</returns>
    /// <remarks>
    /// Uses a 365-day year approximation. Age is floor((currentDay - birthDay) / 365).
    /// A person born on day 0 is age 0 on days 0-364, age 1 on day 365, etc.
    /// </remarks>
    public int GetAge(long currentSimulationDay)
    {
        long ageDays = currentSimulationDay - BirthDayIndex;
        if (ageDays < 0)
        {
            // Current day is before birth - shouldn't happen in valid simulation
            return 0;
        }
        return (int)(ageDays / DaysPerYear);
    }

    /// <summary>
    /// Creates a new Person with a different household.
    /// </summary>
    public Person WithHouseholdId(HouseholdId householdId)
    {
        return new Person(Id, householdId, BirthDayIndex, PersonalAccountId);
    }

    /// <summary>
    /// Creates a new Person with a personal account.
    /// </summary>
    public Person WithPersonalAccountId(AccountId personalAccountId)
    {
        return new Person(Id, HouseholdId, BirthDayIndex, personalAccountId);
    }

    /// <summary>
    /// Creates a new Person with a different birth day index.
    /// </summary>
    public Person WithBirthDayIndex(long birthDayIndex)
    {
        return new Person(Id, HouseholdId, birthDayIndex, PersonalAccountId);
    }

    /// <summary>
    /// Creates a Person with the specified age at the given simulation day.
    /// Convenience factory for testing and initialization.
    /// </summary>
    /// <param name="id">Unique person identifier.</param>
    /// <param name="householdId">Household this person belongs to.</param>
    /// <param name="ageInYears">Age in years at the reference day.</param>
    /// <param name="referenceSimulationDay">The simulation day at which the person has the specified age.</param>
    /// <param name="personalAccountId">Personal monetary account, if any.</param>
    public static Person WithAge(PersonId id, HouseholdId householdId, int ageInYears, long referenceSimulationDay, AccountId personalAccountId = default)
    {
        if (ageInYears < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ageInYears), ageInYears, "Age cannot be negative.");
        }
        long birthDayIndex = referenceSimulationDay - (ageInYears * DaysPerYear);
        return new Person(id, householdId, birthDayIndex, personalAccountId);
    }

    public bool Equals(Person other) =>
        Id.Equals(other.Id) &&
        HouseholdId.Equals(other.HouseholdId) &&
        BirthDayIndex == other.BirthDayIndex &&
        PersonalAccountId.Equals(other.PersonalAccountId);

    public override bool Equals(object? obj) => obj is Person other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Id, HouseholdId, BirthDayIndex, PersonalAccountId);

    public static bool operator ==(Person left, Person right) => left.Equals(right);

    public static bool operator !=(Person left, Person right) => !left.Equals(right);

    public override string ToString()
    {
        if (IsNone) return "Person(none)";
        string baseStr = PersonalAccountId.IsNone
            ? $"Person(#{Id.EntityId.Value}, birthDay={BirthDayIndex}, household=#{HouseholdId.EntityId.Value})"
            : $"Person(#{Id.EntityId.Value}, birthDay={BirthDayIndex}, household=#{HouseholdId.EntityId.Value}, account=#{PersonalAccountId.EntityId.Value})";
        return baseStr;
    }
}
