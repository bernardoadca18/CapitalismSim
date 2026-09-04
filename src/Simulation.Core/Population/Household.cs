using CapitalismSim.Simulation.Core.Entities;

namespace CapitalismSim.Simulation.Core.Population;

/// <summary>
/// Represents a household (family or group of individuals sharing finances).
/// </summary>
/// <remarks>
/// A household has a shared monetary account and a set of members.
/// This is a minimal representation for Phase 2; future phases will add
/// housing, consumption patterns, and other attributes.
/// </remarks>
public sealed class Household
{
    public HouseholdId Id { get; }
    public AccountId AccountId { get; }
    private readonly HashSet<PersonId> _members = new();

    public Household(HouseholdId id, AccountId accountId)
    {
        Id = id;
        AccountId = accountId;
    }

    /// <summary>The members of this household.</summary>
    public IReadOnlySet<PersonId> Members => _members;

    /// <summary>Number of members in this household.</summary>
    public int MemberCount => _members.Count;

    /// <summary>Adds a member to the household.</summary>
    public void AddMember(PersonId personId)
    {
        if (personId.IsNone)
        {
            throw new ArgumentException("Cannot add PersonId.None to a household.", nameof(personId));
        }

        if (!_members.Add(personId))
        {
            throw new InvalidOperationException($"Person {personId} is already a member of household {Id}.");
        }
    }

    /// <summary>Removes a member from the household.</summary>
    public void RemoveMember(PersonId personId)
    {
        if (!_members.Remove(personId))
        {
            throw new InvalidOperationException($"Person {personId} is not a member of household {Id}.");
        }
    }

    /// <summary>Checks if a person is a member of this household.</summary>
    public bool HasMember(PersonId personId) => _members.Contains(personId);
}
