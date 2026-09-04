using CapitalismSim.Simulation.Core.Entities;

namespace CapitalismSim.Simulation.Core.Population;

/// <summary>
/// Manages the population of the simulation, supporting both detailed individuals
/// and aggregated cohorts for Level-of-Detail (LOD) optimization.
/// </summary>
/// <remarks>
/// The simulation can represent population at two resolutions:
/// 1. Detailed individuals: full Person records with household membership
/// 2. Aggregated cohorts: counts of individuals grouped by household or region
///
/// This allows the simulation to scale to very large populations while keeping
/// the number of detailed entities manageable. The LOD strategy is deterministic:
/// the same seed and operations will always produce the same population state.
///
/// Design invariants:
/// - A person belongs to at most one household.
/// - A household has one shared monetary account.
/// - Cohorts and individuals can coexist; cohorts represent "bulk" population
///   that doesn't need individual tracking.
/// </remarks>
public sealed class PopulationState
{
    private readonly Dictionary<PersonId, Person> _individuals = new();
    private readonly Dictionary<HouseholdId, Household> _households = new();
    private readonly Dictionary<HouseholdId, PopulationCohort> _cohorts = new();

    /// <summary>Number of detailed individuals tracked.</summary>
    public int IndividualCount => _individuals.Count;

    /// <summary>Number of households.</summary>
    public int HouseholdCount => _households.Count;

    /// <summary>Number of cohorts (aggregated population groups).</summary>
    public int CohortCount => _cohorts.Count;

    /// <summary>Total population across all individuals and cohorts.</summary>
    public long TotalPopulation
    {
        get
        {
            long total = _individuals.Count;
            foreach (var cohort in _cohorts.Values)
            {
                total = checked(total + cohort.Count);
            }
            return total;
        }
    }

    /// <summary>Gets all individual person IDs.</summary>
    public IEnumerable<PersonId> GetAllPersonIds() => _individuals.Keys;

    /// <summary>Gets all household IDs.</summary>
    public IEnumerable<HouseholdId> GetAllHouseholdIds() => _households.Keys;

    /// <summary>
    /// Adds a person to the population.
    /// </summary>
    public void AddPerson(Person person)
    {
        if (person.IsNone)
        {
            throw new ArgumentException("Cannot add Person.None to population.", nameof(person));
        }

        if (_individuals.ContainsKey(person.Id))
        {
            throw new InvalidOperationException($"Person {person.Id} already exists in population.");
        }

        _individuals[person.Id] = person;

        // Also add to household if specified
        if (!person.HouseholdId.IsNone)
        {
            if (_households.TryGetValue(person.HouseholdId, out var household))
            {
                household.AddMember(person.Id);
            }
        }
    }

    /// <summary>
    /// Gets a person by ID. Returns Person.None if not found.
    /// </summary>
    public Person GetPerson(PersonId personId)
    {
        return _individuals.TryGetValue(personId, out var person) ? person : Person.None;
    }

    /// <summary>
    /// Checks if a person exists in the population.
    /// </summary>
    public bool PersonExists(PersonId personId) => _individuals.ContainsKey(personId);

    /// <summary>
    /// Creates a new household with the given account.
    /// </summary>
    public void CreateHousehold(HouseholdId householdId, AccountId accountId)
    {
        if (_households.ContainsKey(householdId))
        {
            throw new InvalidOperationException($"Household {householdId} already exists.");
        }

        _households[householdId] = new Household(householdId, accountId);
    }

    /// <summary>
    /// Gets a household by ID. Returns null if not found.
    /// </summary>
    public Household? GetHousehold(HouseholdId householdId)
    {
        return _households.TryGetValue(householdId, out var household) ? household : null;
    }

    /// <summary>
    /// Checks if a household exists.
    /// </summary>
    public bool HouseholdExists(HouseholdId householdId) => _households.ContainsKey(householdId);

    /// <summary>
    /// Adds or updates a population cohort.
    /// </summary>
    public void SetCohort(HouseholdId householdId, PopulationCohort cohort)
    {
        if (cohort.HouseholdId != householdId)
        {
            throw new ArgumentException(
                $"Cohort's household ID ({cohort.HouseholdId}) doesn't match key ({householdId}).",
                nameof(cohort));
        }

        _cohorts[householdId] = cohort;
    }

    /// <summary>
    /// Gets a cohort by household ID. Returns empty cohort if not found.
    /// </summary>
    public PopulationCohort GetCohort(HouseholdId householdId)
    {
        return _cohorts.TryGetValue(householdId, out var cohort)
            ? cohort
            : PopulationCohort.Empty(householdId);
    }

    /// <summary>
    /// Removes a cohort. Returns true if it existed.
    /// </summary>
    public bool RemoveCohort(HouseholdId householdId) => _cohorts.Remove(householdId);
}
