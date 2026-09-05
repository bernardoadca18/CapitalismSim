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
    private readonly Dictionary<PopulationCohortId, PopulationCohort> _cohorts = new();
    private readonly Dictionary<HouseholdId, HouseholdNeedsState> _householdNeeds = new();

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
    /// <remarks>
    /// This operation is atomic: either the person is fully added (including household
    /// membership if specified) or no state is changed. Pre-validates all constraints
    /// before any mutation.
    /// </remarks>
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

        // Pre-validate household membership before any mutation
        Household? household = null;
        if (!person.HouseholdId.IsNone)
        {
            household = GetHousehold(person.HouseholdId);
            if (household is null)
            {
                throw new InvalidOperationException(
                    $"Cannot add person {person.Id} to non-existent household {person.HouseholdId}.");
            }

            if (household.HasMember(person.Id))
            {
                throw new InvalidOperationException(
                    $"Person {person.Id} is already a member of household {person.HouseholdId}.");
            }
        }

        // All validations passed - perform mutations atomically
        _individuals[person.Id] = person;
        household?.AddMember(person.Id);
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
    /// Removes a person from the population.
    /// </summary>
    /// <remarks>
    /// This operation is atomic: either the person is fully removed (including from
    /// household membership) or no state is changed. Pre-validates all constraints
    /// before any mutation.
    /// </remarks>
    public void RemovePerson(PersonId personId)
    {
        if (personId.IsNone)
        {
            throw new ArgumentException("Cannot remove PersonId.None from population.", nameof(personId));
        }

        var person = GetPerson(personId);
        if (person.IsNone)
        {
            throw new InvalidOperationException($"Person {personId} does not exist in population.");
        }

        // Pre-validate household membership removal
        Household? household = null;
        if (!person.HouseholdId.IsNone)
        {
            household = GetHousehold(person.HouseholdId);
            if (household is not null && !household.HasMember(personId))
            {
                throw new InvalidOperationException(
                    $"Person {personId} is not a member of household {person.HouseholdId}.");
            }
        }

        // All validations passed - perform mutations atomically
        _individuals.Remove(personId);
        household?.RemoveMember(personId);
    }

    /// <summary>
    /// Atomically adds multiple people to the population.
    /// Pre-validates all people before any mutation.
    /// </summary>
    /// <remarks>
    /// Detects duplicate IDs WITHIN the batch before any mutation.
    /// An invalid batch will not partially modify state.
    /// </remarks>
    public void AddPeople(IReadOnlyList<Person> people)
    {
        ArgumentNullException.ThrowIfNull(people);

        if (people.Count == 0)
        {
            return;
        }

        // Detect duplicates WITHIN the batch first
        var batchIds = new HashSet<PersonId>(people.Count);
        foreach (var person in people)
        {
            if (person.IsNone)
            {
                throw new ArgumentException("Cannot add Person.None to population.", nameof(people));
            }

            if (!batchIds.Add(person.Id))
            {
                throw new InvalidOperationException(
                    $"Batch contains duplicate person ID {person.Id}.");
            }
        }

        // Pre-validate all people against existing state
        foreach (var person in people)
        {
            if (_individuals.ContainsKey(person.Id))
            {
                throw new InvalidOperationException($"Person {person.Id} already exists in population.");
            }

            if (!person.HouseholdId.IsNone)
            {
                var household = GetHousehold(person.HouseholdId);
                if (household is null)
                {
                    throw new InvalidOperationException(
                        $"Cannot add person {person.Id} to non-existent household {person.HouseholdId}.");
                }

                if (household.HasMember(person.Id))
                {
                    throw new InvalidOperationException(
                        $"Person {person.Id} is already a member of household {person.HouseholdId}.");
                }
            }
        }

        // All validations passed - perform mutations atomically
        foreach (var person in people)
        {
            _individuals[person.Id] = person;
            if (!person.HouseholdId.IsNone)
            {
                GetHousehold(person.HouseholdId)?.AddMember(person.Id);
            }
        }
    }

    /// <summary>
    /// Atomically moves a person from one household to another.
    /// Pre-validates all constraints before any mutation.
    /// </summary>
    /// <remarks>
    /// This operation:
    /// 1. Validates the person exists and currently belongs to the source household
    /// 2. Validates the destination household exists
    /// 3. Updates the person's HouseholdId
    /// 4. Removes the person from the source household's member list
    /// 5. Adds the person to the destination household's member list
    ///
    /// If any validation fails, no state is changed.
    /// </remarks>
    public void MovePerson(PersonId personId, HouseholdId destinationHouseholdId)
    {
        if (personId.IsNone)
        {
            throw new ArgumentException("Cannot move PersonId.None.", nameof(personId));
        }

        if (destinationHouseholdId.IsNone)
        {
            throw new ArgumentException("Cannot move person to HouseholdId.None.", nameof(destinationHouseholdId));
        }

        // Pre-validate: person exists
        var person = GetPerson(personId);
        if (person.IsNone)
        {
            throw new InvalidOperationException($"Person {personId} does not exist in population.");
        }

        // Pre-validate: source household exists and person is a member
        if (person.HouseholdId.IsNone)
        {
            throw new InvalidOperationException(
                $"Person {personId} is not a member of any household.");
        }

        var sourceHousehold = GetHousehold(person.HouseholdId);
        if (sourceHousehold is null)
        {
            throw new InvalidOperationException(
                $"Source household {person.HouseholdId} does not exist.");
        }

        if (!sourceHousehold.HasMember(personId))
        {
            throw new InvalidOperationException(
                $"Person {personId} is not a member of household {person.HouseholdId}.");
        }

        // Pre-validate: destination household exists
        var destHousehold = GetHousehold(destinationHouseholdId);
        if (destHousehold is null)
        {
            throw new InvalidOperationException(
                $"Destination household {destinationHouseholdId} does not exist.");
        }

        // Pre-validate: person is not already in destination household
        if (destHousehold.HasMember(personId))
        {
            throw new InvalidOperationException(
                $"Person {personId} is already a member of destination household {destinationHouseholdId}.");
        }

        // All validations passed - perform mutations atomically
        // 1. Update person's HouseholdId
        var updatedPerson = person.WithHouseholdId(destinationHouseholdId);
        _individuals[personId] = updatedPerson;

        // 2. Remove from source household
        sourceHousehold.RemoveMember(personId);

        // 3. Add to destination household
        destHousehold.AddMember(personId);
    }

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
    public void SetCohort(PopulationCohort cohort)
    {
        if (cohort.Id.IsNone)
        {
            throw new ArgumentException("Cannot set a cohort with PopulationCohortId.None.", nameof(cohort));
        }

        _cohorts[cohort.Id] = cohort;
    }

    /// <summary>
    /// Gets a cohort by ID. Returns empty cohort if not found.
    /// </summary>
    public PopulationCohort GetCohort(PopulationCohortId cohortId)
    {
        return _cohorts.TryGetValue(cohortId, out var cohort)
            ? cohort
            : PopulationCohort.Empty(cohortId);
    }

    /// <summary>
    /// Removes a cohort. Returns true if it existed.
    /// </summary>
    public bool RemoveCohort(PopulationCohortId cohortId) => _cohorts.Remove(cohortId);

    /// <summary>
    /// Checks if a cohort exists.
    /// </summary>
    public bool CohortExists(PopulationCohortId cohortId) => _cohorts.ContainsKey(cohortId);

    /// <summary>
    /// Gets all cohort IDs.
    /// </summary>
    public IEnumerable<PopulationCohortId> GetAllCohortIds() => _cohorts.Keys;

    #region Household Needs State

    /// <summary>
    /// Sets the needs state for a household.
    /// </summary>
    public void SetHouseholdNeedsState(HouseholdNeedsState needsState)
    {
        ArgumentNullException.ThrowIfNull(needsState);
        _householdNeeds[needsState.HouseholdId] = needsState;
    }

    /// <summary>
    /// Gets the needs state for a household. Returns null if not tracked.
    /// </summary>
    public HouseholdNeedsState? GetHouseholdNeedsState(HouseholdId householdId)
    {
        return _householdNeeds.TryGetValue(householdId, out var state) ? state : null;
    }

    /// <summary>
    /// Gets all household IDs that have needs state tracked.
    /// </summary>
    public IEnumerable<HouseholdId> GetAllHouseholdIdsWithNeedsState() => _householdNeeds.Keys;

    #endregion

    #region Validation

    /// <summary>
    /// Validates that all detailed persons with personal accounts have correct ownership.
    /// </summary>
    /// <param name="ledger">The money ledger to validate account ownership against.</param>
    /// <returns>A validation result indicating success or listing all violations.</returns>
    /// <remarks>
    /// This validates that for each person with a non-none PersonalAccountId:
    /// - The account exists in the ledger
    /// - The account's owner matches the person's EntityId
    ///
    /// This should be called at validation boundaries (e.g., after loading a snapshot).
    /// </remarks>
    public PopulationValidationResult ValidatePersonalAccounts(Economics.MoneyLedger ledger)
    {
        ArgumentNullException.ThrowIfNull(ledger);

        var violations = new List<string>();

        foreach (var (personId, person) in _individuals)
        {
            if (person.PersonalAccountId.IsNone)
            {
                continue; // No personal account, nothing to validate
            }

            var accountId = person.PersonalAccountId;

            // Check that the account exists
            var owner = ledger.GetOwner(accountId);
            if (owner.IsNone)
            {
                violations.Add(
                    $"Person {personId} has personal account {accountId} which does not exist in ledger.");
                continue;
            }

            // Check that the owner matches
            if (owner != personId.EntityId)
            {
                violations.Add(
                    $"Person {personId} has personal account {accountId} owned by {owner}, expected {personId.EntityId}.");
            }
        }

        return violations.Count == 0
            ? PopulationValidationResult.Success()
            : PopulationValidationResult.Failure(violations);
    }

    /// <summary>
    /// Validates the integrity of the entire population state.
    /// </summary>
    /// <param name="ledger">The money ledger to validate account ownership against.</param>
    /// <returns>A validation result indicating success or listing all violations.</returns>
    /// <remarks>
    /// Performs comprehensive validation including:
    /// - Personal account ownership
    /// - Household membership consistency (person's HouseholdId matches household's member list)
    /// - No orphaned persons (person references non-existent household)
    /// - No phantom members (household references non-existent person)
    /// </remarks>
    public PopulationValidationResult ValidateIntegrity(Economics.MoneyLedger ledger)
    {
        ArgumentNullException.ThrowIfNull(ledger);

        var violations = new List<string>();

        // Validate personal accounts
        var accountResult = ValidatePersonalAccounts(ledger);
        if (!accountResult.IsValid)
        {
            violations.AddRange(accountResult.Violations);
        }

        // Validate household membership consistency
        foreach (var (personId, person) in _individuals)
        {
            if (person.HouseholdId.IsNone)
            {
                continue; // Person without household is valid
            }

            var household = GetHousehold(person.HouseholdId);
            if (household is null)
            {
                violations.Add(
                    $"Person {personId} references non-existent household {person.HouseholdId}.");
                continue;
            }

            if (!household.HasMember(personId))
            {
                violations.Add(
                    $"Person {personId} has HouseholdId {person.HouseholdId} but is not in that household's member list.");
            }
        }

        // Validate no phantom members
        foreach (var (householdId, household) in _households)
        {
            foreach (var memberId in household.Members)
            {
                if (!_individuals.ContainsKey(memberId))
                {
                    violations.Add(
                        $"Household {householdId} has member {memberId} who does not exist in population.");
                }
            }
        }

        return violations.Count == 0
            ? PopulationValidationResult.Success()
            : PopulationValidationResult.Failure(violations);
    }

    #endregion

    /// <summary>
    /// Creates a deep copy of this population state.
    /// </summary>
    public PopulationState Clone()
    {
        var clone = new PopulationState();

        // Clone households (they have mutable member sets)
        foreach (var (householdId, household) in _households)
        {
            var clonedHousehold = new Household(household.Id, household.AccountId);
            foreach (var memberId in household.Members)
            {
                clonedHousehold.AddMember(memberId);
            }
            clone._households[householdId] = clonedHousehold;
        }

        // Clone individuals (structs are copied by value)
        foreach (var (personId, person) in _individuals)
        {
            clone._individuals[personId] = person;
        }

        // Clone cohorts (structs are copied by value)
        foreach (var (cohortId, cohort) in _cohorts)
        {
            clone._cohorts[cohortId] = cohort;
        }

        // Clone household needs states (classes with mutable state)
        foreach (var (householdId, needsState) in _householdNeeds)
        {
            clone._householdNeeds[householdId] = needsState.Clone();
        }

        return clone;
    }
}
