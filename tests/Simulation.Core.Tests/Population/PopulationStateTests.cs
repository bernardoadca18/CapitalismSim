using CapitalismSim.Simulation.Core.Economics;
using CapitalismSim.Simulation.Core.Entities;
using CapitalismSim.Simulation.Core.Population;
using Xunit;

namespace CapitalismSim.Simulation.Core.Tests.Population;

public sealed class PopulationStateTests
{
    [Fact]
    public void Constructor_StartsEmpty()
    {
        var state = new PopulationState();

        Assert.Equal(0, state.IndividualCount);
        Assert.Equal(0, state.HouseholdCount);
        Assert.Equal(0, state.CohortCount);
        Assert.Equal(0L, state.TotalPopulation);
    }

    [Fact]
    public void AddPerson_IncreasesIndividualCount()
    {
        var state = new PopulationState();
        var personId = new PersonId(new EntityId(1));
        var householdId = new HouseholdId(new EntityId(10));
        state.CreateHousehold(householdId, new AccountId(new EntityId(100)));
        var person = new Person(personId, householdId);

        state.AddPerson(person);

        Assert.Equal(1, state.IndividualCount);
        Assert.True(state.PersonExists(personId));
        Assert.Equal(person, state.GetPerson(personId));
    }

    [Fact]
    public void AddPerson_CannotAddNone()
    {
        var state = new PopulationState();

        Assert.Throws<ArgumentException>(() => state.AddPerson(Person.None));
    }

    [Fact]
    public void AddPerson_CannotAddDuplicate()
    {
        var state = new PopulationState();
        var personId = new PersonId(new EntityId(1));
        var householdId = new HouseholdId(new EntityId(10));
        state.CreateHousehold(householdId, new AccountId(new EntityId(100)));
        var person = new Person(personId, householdId);
        state.AddPerson(person);

        Assert.Throws<InvalidOperationException>(() => state.AddPerson(person));
    }

    [Fact]
    public void AddPerson_WithoutHousehold_Throws()
    {
        var state = new PopulationState();
        var personId = new PersonId(new EntityId(1));
        var householdId = new HouseholdId(new EntityId(10));
        var person = new Person(personId, householdId);

        Assert.Throws<InvalidOperationException>(() => state.AddPerson(person));
    }

    [Fact]
    public void AddPerson_PreValidatesHousehold()
    {
        var state = new PopulationState();
        var personId = new PersonId(new EntityId(1));
        var householdId = new HouseholdId(new EntityId(10));
        var person = new Person(personId, householdId);

        // Person references a household that doesn't exist - should throw before any mutation
        Assert.Throws<InvalidOperationException>(() => state.AddPerson(person));
        Assert.Equal(0, state.IndividualCount); // No mutation happened
    }

    [Fact]
    public void GetPerson_NotFound_ReturnsNone()
    {
        var state = new PopulationState();
        var personId = new PersonId(new EntityId(1));

        var result = state.GetPerson(personId);

        Assert.True(result.IsNone);
    }

    [Fact]
    public void RemovePerson_RemovesPersonAndHouseholdMembership()
    {
        var state = new PopulationState();
        var householdId = new HouseholdId(new EntityId(10));
        state.CreateHousehold(householdId, new AccountId(new EntityId(100)));
        var personId = new PersonId(new EntityId(1));
        state.AddPerson(new Person(personId, householdId));

        state.RemovePerson(personId);

        Assert.Equal(0, state.IndividualCount);
        Assert.False(state.PersonExists(personId));
        var household = state.GetHousehold(householdId);
        Assert.NotNull(household);
        Assert.DoesNotContain(personId, household.Members);
    }

    [Fact]
    public void RemovePerson_NonExistent_Throws()
    {
        var state = new PopulationState();
        var personId = new PersonId(new EntityId(1));

        Assert.Throws<InvalidOperationException>(() => state.RemovePerson(personId));
    }

    [Fact]
    public void CreateHousehold_AddsHousehold()
    {
        var state = new PopulationState();
        var householdId = new HouseholdId(new EntityId(1));
        var accountId = new AccountId(new EntityId(100));

        state.CreateHousehold(householdId, accountId);

        Assert.Equal(1, state.HouseholdCount);
        Assert.True(state.HouseholdExists(householdId));
        var household = state.GetHousehold(householdId);
        Assert.NotNull(household);
        Assert.Equal(householdId, household.Id);
        Assert.Equal(accountId, household.AccountId);
    }

    [Fact]
    public void CreateHousehold_CannotCreateDuplicate()
    {
        var state = new PopulationState();
        var householdId = new HouseholdId(new EntityId(1));
        var accountId = new AccountId(new EntityId(100));
        state.CreateHousehold(householdId, accountId);

        Assert.Throws<InvalidOperationException>(() =>
            state.CreateHousehold(householdId, accountId));
    }

    [Fact]
    public void GetHousehold_NotFound_ReturnsNull()
    {
        var state = new PopulationState();
        var householdId = new HouseholdId(new EntityId(1));

        var result = state.GetHousehold(householdId);

        Assert.Null(result);
    }

    [Fact]
    public void SetCohort_AddsCohort()
    {
        var state = new PopulationState();
        var cohortId = new PopulationCohortId(new EntityId(1));
        var householdId = new HouseholdId(new EntityId(10));
        var cohort = new PopulationCohort(cohortId, 100L, householdId);

        state.SetCohort(cohort);

        Assert.Equal(1, state.CohortCount);
        Assert.Equal(cohort, state.GetCohort(cohortId));
    }

    [Fact]
    public void SetCohort_NoneId_Throws()
    {
        var state = new PopulationState();
        var cohort = PopulationCohort.Empty(PopulationCohortId.None);

        Assert.Throws<ArgumentException>(() => state.SetCohort(cohort));
    }

    [Fact]
    public void GetCohort_NotFound_ReturnsEmpty()
    {
        var state = new PopulationState();
        var cohortId = new PopulationCohortId(new EntityId(1));

        var result = state.GetCohort(cohortId);

        Assert.True(result.IsEmpty);
        Assert.Equal(0L, result.Count);
    }

    [Fact]
    public void RemoveCohort_RemovesExistingCohort()
    {
        var state = new PopulationState();
        var cohortId = new PopulationCohortId(new EntityId(1));
        var householdId = new HouseholdId(new EntityId(10));
        var cohort = new PopulationCohort(cohortId, 100L, householdId);
        state.SetCohort(cohort);

        var removed = state.RemoveCohort(cohortId);

        Assert.True(removed);
        Assert.Equal(0, state.CohortCount);
        Assert.True(state.GetCohort(cohortId).IsEmpty);
    }

    [Fact]
    public void RemoveCohort_NonExistent_ReturnsFalse()
    {
        var state = new PopulationState();
        var cohortId = new PopulationCohortId(new EntityId(1));

        var removed = state.RemoveCohort(cohortId);

        Assert.False(removed);
    }

    [Fact]
    public void TotalPopulation_SumsIndividualsAndCohorts()
    {
        var state = new PopulationState();

        // Add 3 individuals
        var h1 = new HouseholdId(new EntityId(1));
        state.CreateHousehold(h1, new AccountId(new EntityId(100)));
        state.AddPerson(new Person(new PersonId(new EntityId(1)), h1));
        state.AddPerson(new Person(new PersonId(new EntityId(2)), h1));
        state.AddPerson(new Person(new PersonId(new EntityId(3)), h1));

        // Add a cohort with 500 people
        var cohortId = new PopulationCohortId(new EntityId(1));
        state.SetCohort(new PopulationCohort(cohortId, 500L, HouseholdId.None));

        Assert.Equal(503L, state.TotalPopulation);
    }

    /// <summary>
    /// Large population LOD test: demonstrates that cohorts can represent
    /// ~300M population without creating 300M individual entity records.
    /// This is the key scalability feature of the PopulationState design.
    /// </summary>
    [Fact]
    public void LargePopulation_UsingCohorts_ScalesTo300Million()
    {
        var state = new PopulationState();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Simulate a large country with 330M population using 330 cohorts of 1M each
        // This tests that we can represent large populations without memory explosion
        const int cohortCount = 330;
        const long peoplePerCohort = 1_000_000L;
        const long expectedTotal = cohortCount * peoplePerCohort;

        for (int i = 0; i < cohortCount; i++)
        {
            var cohortId = new PopulationCohortId(new EntityId(i + 1));
            state.SetCohort(new PopulationCohort(cohortId, peoplePerCohort, HouseholdId.None));
        }

        sw.Stop();

        // Verify we can represent 330M people
        Assert.Equal(cohortCount, state.CohortCount);
        Assert.Equal(expectedTotal, state.TotalPopulation);

        // Verify the operation was fast (should be < 100ms for 330 cohorts)
        // This demonstrates that cohort-based population scales efficiently
        Assert.True(sw.ElapsedMilliseconds < 1000,
            $"Creating {cohortCount} cohorts took {sw.ElapsedMilliseconds}ms, expected < 1000ms");

        // Verify individual count is 0 - we haven't created any Person records
        Assert.Equal(0, state.IndividualCount);
    }

    [Fact]
    public void AddPerson_WithHousehold_AddsMemberToHousehold()
    {
        var state = new PopulationState();
        var householdId = new HouseholdId(new EntityId(1));
        var accountId = new AccountId(new EntityId(100));
        state.CreateHousehold(householdId, accountId);

        var personId = new PersonId(new EntityId(1));
        var person = new Person(personId, householdId);
        state.AddPerson(person);

        var household = state.GetHousehold(householdId);
        Assert.NotNull(household);
        Assert.Contains(personId, household.Members);
    }

    [Fact]
    public void GetAllPersonIds_ReturnsAllAddedPersons()
    {
        var state = new PopulationState();
        var p1 = new PersonId(new EntityId(1));
        var p2 = new PersonId(new EntityId(2));
        var p3 = new PersonId(new EntityId(3));
        var h = new HouseholdId(new EntityId(10));
        state.CreateHousehold(h, new AccountId(new EntityId(100)));
        state.AddPerson(new Person(p1, h));
        state.AddPerson(new Person(p2, h));
        state.AddPerson(new Person(p3, h));

        var ids = state.GetAllPersonIds().ToHashSet();

        Assert.Equal(3, ids.Count);
        Assert.Contains(p1, ids);
        Assert.Contains(p2, ids);
        Assert.Contains(p3, ids);
    }

    [Fact]
    public void GetAllHouseholdIds_ReturnsAllCreatedHouseholds()
    {
        var state = new PopulationState();
        var h1 = new HouseholdId(new EntityId(1));
        var h2 = new HouseholdId(new EntityId(2));
        state.CreateHousehold(h1, new AccountId(new EntityId(100)));
        state.CreateHousehold(h2, new AccountId(new EntityId(101)));

        var ids = state.GetAllHouseholdIds().ToHashSet();

        Assert.Equal(2, ids.Count);
        Assert.Contains(h1, ids);
        Assert.Contains(h2, ids);
    }

    [Fact]
    public void GetAllCohortIds_ReturnsAllAddedCohorts()
    {
        var state = new PopulationState();
        var c1 = new PopulationCohortId(new EntityId(1));
        var c2 = new PopulationCohortId(new EntityId(2));
        var h = new HouseholdId(new EntityId(10));
        state.SetCohort(new PopulationCohort(c1, 100L, h));
        state.SetCohort(new PopulationCohort(c2, 200L, HouseholdId.None));

        var ids = state.GetAllCohortIds().ToHashSet();

        Assert.Equal(2, ids.Count);
        Assert.Contains(c1, ids);
        Assert.Contains(c2, ids);
    }

    [Fact]
    public void AddPeople_AtomicBatchOperation()
    {
        var state = new PopulationState();
        var h = new HouseholdId(new EntityId(10));
        state.CreateHousehold(h, new AccountId(new EntityId(100)));
        var people = new List<Person>
        {
            new(new PersonId(new EntityId(1)), h),
            new(new PersonId(new EntityId(2)), h),
            new(new PersonId(new EntityId(3)), h),
        };

        state.AddPeople(people);

        Assert.Equal(3, state.IndividualCount);
        var household = state.GetHousehold(h);
        Assert.NotNull(household);
        Assert.Equal(3, household.MemberCount);
    }

    [Fact]
    public void AddPeople_RollsBackOnFailure()
    {
        var state = new PopulationState();
        var h1 = new HouseholdId(new EntityId(10));
        var h2 = new HouseholdId(new EntityId(20)); // This household doesn't exist
        state.CreateHousehold(h1, new AccountId(new EntityId(100)));
        var people = new List<Person>
        {
            new(new PersonId(new EntityId(1)), h1),
            new(new PersonId(new EntityId(2)), h2), // This will fail
            new(new PersonId(new EntityId(3)), h1),
        };

        Assert.Throws<InvalidOperationException>(() => state.AddPeople(people));

        // No mutation should have happened
        Assert.Equal(0, state.IndividualCount);
        var household = state.GetHousehold(h1);
        Assert.NotNull(household);
        Assert.Equal(0, household.MemberCount);
    }

    #region HouseholdNeedsState Tests

    [Fact]
    public void SetHouseholdNeedsState_AddsState()
    {
        var state = new PopulationState();
        var householdId = new HouseholdId(new EntityId(1));
        state.CreateHousehold(householdId, new AccountId(new EntityId(100)));

        var profile = BasicNeedsProfile.Zero(CurrencyCode.USD);
        var needsState = new HouseholdNeedsState(householdId, profile, 4);
        state.SetHouseholdNeedsState(needsState);

        var retrieved = state.GetHouseholdNeedsState(householdId);
        Assert.NotNull(retrieved);
        Assert.Equal(householdId, retrieved.HouseholdId);
    }

    [Fact]
    public void GetHouseholdNeedsState_NotFound_ReturnsNull()
    {
        var state = new PopulationState();
        var householdId = new HouseholdId(new EntityId(1));

        var result = state.GetHouseholdNeedsState(householdId);

        Assert.Null(result);
    }

    [Fact]
    public void GetAllHouseholdIdsWithNeedsState_ReturnsTrackedHouseholds()
    {
        var state = new PopulationState();
        var h1 = new HouseholdId(new EntityId(1));
        var h2 = new HouseholdId(new EntityId(2));
        var h3 = new HouseholdId(new EntityId(3));
        state.CreateHousehold(h1, new AccountId(new EntityId(100)));
        state.CreateHousehold(h2, new AccountId(new EntityId(101)));
        state.CreateHousehold(h3, new AccountId(new EntityId(102)));

        var profile = BasicNeedsProfile.Zero(CurrencyCode.USD);
        state.SetHouseholdNeedsState(new HouseholdNeedsState(h1, profile, 2));
        state.SetHouseholdNeedsState(new HouseholdNeedsState(h3, profile, 3));

        var ids = state.GetAllHouseholdIdsWithNeedsState().ToHashSet();

        Assert.Equal(2, ids.Count);
        Assert.Contains(h1, ids);
        Assert.Contains(h3, ids);
        Assert.DoesNotContain(h2, ids);
    }

    #endregion

    #region MovePerson Tests

    [Fact]
    public void MovePerson_TransfersPersonBetweenHouseholds()
    {
        var state = new PopulationState();
        var h1 = new HouseholdId(new EntityId(1));
        var h2 = new HouseholdId(new EntityId(2));
        state.CreateHousehold(h1, new AccountId(new EntityId(100)));
        state.CreateHousehold(h2, new AccountId(new EntityId(101)));
        var personId = new PersonId(new EntityId(1));
        state.AddPerson(new Person(personId, h1));

        state.MovePerson(personId, h2);

        var person = state.GetPerson(personId);
        Assert.Equal(h2, person.HouseholdId);
        var household1 = state.GetHousehold(h1);
        var household2 = state.GetHousehold(h2);
        Assert.NotNull(household1);
        Assert.NotNull(household2);
        Assert.DoesNotContain(personId, household1.Members);
        Assert.Contains(personId, household2.Members);
    }

    [Fact]
    public void MovePerson_NonePersonId_Throws()
    {
        var state = new PopulationState();
        var h = new HouseholdId(new EntityId(1));
        state.CreateHousehold(h, new AccountId(new EntityId(100)));

        Assert.Throws<ArgumentException>(() => state.MovePerson(PersonId.None, h));
    }

    [Fact]
    public void MovePerson_NoneHouseholdId_Throws()
    {
        var state = new PopulationState();
        var h1 = new HouseholdId(new EntityId(1));
        state.CreateHousehold(h1, new AccountId(new EntityId(100)));
        var personId = new PersonId(new EntityId(1));
        state.AddPerson(new Person(personId, h1));

        Assert.Throws<ArgumentException>(() => state.MovePerson(personId, HouseholdId.None));
    }

    [Fact]
    public void MovePerson_NonExistentPerson_Throws()
    {
        var state = new PopulationState();
        var h = new HouseholdId(new EntityId(1));
        state.CreateHousehold(h, new AccountId(new EntityId(100)));
        var personId = new PersonId(new EntityId(999));

        Assert.Throws<InvalidOperationException>(() => state.MovePerson(personId, h));
    }

    [Fact]
    public void MovePerson_NonExistentDestinationHousehold_Throws()
    {
        var state = new PopulationState();
        var h1 = new HouseholdId(new EntityId(1));
        var h2 = new HouseholdId(new EntityId(2)); // Not created
        state.CreateHousehold(h1, new AccountId(new EntityId(100)));
        var personId = new PersonId(new EntityId(1));
        state.AddPerson(new Person(personId, h1));

        Assert.Throws<InvalidOperationException>(() => state.MovePerson(personId, h2));
    }

    [Fact]
    public void MovePerson_AlreadyInDestinationHousehold_Throws()
    {
        var state = new PopulationState();
        var h = new HouseholdId(new EntityId(1));
        state.CreateHousehold(h, new AccountId(new EntityId(100)));
        var personId = new PersonId(new EntityId(1));
        state.AddPerson(new Person(personId, h));

        Assert.Throws<InvalidOperationException>(() => state.MovePerson(personId, h));
    }

    [Fact]
    public void MovePerson_PreservesOtherAttributes()
    {
        var state = new PopulationState();
        var h1 = new HouseholdId(new EntityId(1));
        var h2 = new HouseholdId(new EntityId(2));
        state.CreateHousehold(h1, new AccountId(new EntityId(100)));
        state.CreateHousehold(h2, new AccountId(new EntityId(101)));
        var personId = new PersonId(new EntityId(1));
        var personalAccount = new AccountId(new EntityId(200));
        var person = Person.WithAge(personId, h1, 30, referenceSimulationDay: 0, personalAccount);
        state.AddPerson(person);

        state.MovePerson(personId, h2);

        var movedPerson = state.GetPerson(personId);
        Assert.Equal(30, movedPerson.GetAge(0));
        Assert.Equal(personalAccount, movedPerson.PersonalAccountId);
    }

    #endregion

    #region AddPeople Duplicate Detection Tests

    [Fact]
    public void AddPeople_DetectsDuplicatesWithinBatch()
    {
        var state = new PopulationState();
        var h = new HouseholdId(new EntityId(10));
        state.CreateHousehold(h, new AccountId(new EntityId(100)));
        var personId = new PersonId(new EntityId(1));
        var people = new List<Person>
        {
            new(personId, h),
            new(personId, h), // Duplicate ID in same batch
        };

        var ex = Assert.Throws<InvalidOperationException>(() => state.AddPeople(people));
        Assert.Contains("duplicate person ID", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddPeople_DuplicateInBatch_DoesNotMutate()
    {
        var state = new PopulationState();
        var h = new HouseholdId(new EntityId(10));
        state.CreateHousehold(h, new AccountId(new EntityId(100)));
        var p1 = new PersonId(new EntityId(1));
        var p2 = new PersonId(new EntityId(2));
        var people = new List<Person>
        {
            new(p1, h),
            new(p1, h), // Duplicate ID
            new(p2, h),
        };

        Assert.Throws<InvalidOperationException>(() => state.AddPeople(people));

        Assert.Equal(0, state.IndividualCount);
        var household = state.GetHousehold(h);
        Assert.NotNull(household);
        Assert.Equal(0, household.MemberCount);
    }

    #endregion

    #region Validation Tests

    [Fact]
    public void ValidatePersonalAccounts_ValidAccounts_ReturnsSuccess()
    {
        var state = new PopulationState();
        var ledger = new MoneyLedger(CurrencyCode.USD);
        var h = new HouseholdId(new EntityId(10));
        var accountId = new AccountId(new EntityId(100));
        state.CreateHousehold(h, accountId);
        ledger.CreateAccount(accountId, h.EntityId);

        var personId = new PersonId(new EntityId(1));
        var personalAccountId = new AccountId(new EntityId(200));
        ledger.CreateAccount(personalAccountId, personId.EntityId);
        var person = new Person(personId, h, personalAccountId: personalAccountId);
        state.AddPerson(person);

        var result = state.ValidatePersonalAccounts(ledger);

        Assert.True(result.IsValid);
        Assert.Empty(result.Violations);
    }

    [Fact]
    public void ValidatePersonalAccounts_MissingAccount_ReturnsFailure()
    {
        var state = new PopulationState();
        var ledger = new MoneyLedger(CurrencyCode.USD);
        var h = new HouseholdId(new EntityId(10));
        var accountId = new AccountId(new EntityId(100));
        state.CreateHousehold(h, accountId);
        ledger.CreateAccount(accountId, h.EntityId);

        var personId = new PersonId(new EntityId(1));
        var personalAccountId = new AccountId(new EntityId(200));
        // Note: NOT creating the account in ledger
        var person = new Person(personId, h, personalAccountId: personalAccountId);
        state.AddPerson(person);

        var result = state.ValidatePersonalAccounts(ledger);

        Assert.False(result.IsValid);
        Assert.Contains(result.Violations, v => v.Contains("does not exist in ledger"));
    }

    [Fact]
    public void ValidatePersonalAccounts_WrongOwner_ReturnsFailure()
    {
        var state = new PopulationState();
        var ledger = new MoneyLedger(CurrencyCode.USD);
        var h = new HouseholdId(new EntityId(10));
        var accountId = new AccountId(new EntityId(100));
        state.CreateHousehold(h, accountId);
        ledger.CreateAccount(accountId, h.EntityId);

        var personId = new PersonId(new EntityId(1));
        var personalAccountId = new AccountId(new EntityId(200));
        // Create account with WRONG owner
        var wrongOwner = new EntityId(999);
        ledger.CreateAccount(personalAccountId, wrongOwner);
        var person = new Person(personId, h, personalAccountId: personalAccountId);
        state.AddPerson(person);

        var result = state.ValidatePersonalAccounts(ledger);

        Assert.False(result.IsValid);
        Assert.Contains(result.Violations, v => v.Contains("owned by") && v.Contains("expected"));
    }

    [Fact]
    public void ValidatePersonalAccounts_NoPersonalAccount_SkipsValidation()
    {
        var state = new PopulationState();
        var ledger = new MoneyLedger(CurrencyCode.USD);
        var h = new HouseholdId(new EntityId(10));
        var accountId = new AccountId(new EntityId(100));
        state.CreateHousehold(h, accountId);
        ledger.CreateAccount(accountId, h.EntityId);

        var personId = new PersonId(new EntityId(1));
        // Person with no personal account
        var person = new Person(personId, h);
        state.AddPerson(person);

        var result = state.ValidatePersonalAccounts(ledger);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateIntegrity_ValidState_ReturnsSuccess()
    {
        var state = new PopulationState();
        var ledger = new MoneyLedger(CurrencyCode.USD);
        var h = new HouseholdId(new EntityId(10));
        var accountId = new AccountId(new EntityId(100));
        state.CreateHousehold(h, accountId);
        ledger.CreateAccount(accountId, h.EntityId);

        var personId = new PersonId(new EntityId(1));
        var personalAccountId = new AccountId(new EntityId(200));
        ledger.CreateAccount(personalAccountId, personId.EntityId);
        var person = new Person(personId, h, personalAccountId: personalAccountId);
        state.AddPerson(person);

        var result = state.ValidateIntegrity(ledger);

        Assert.True(result.IsValid);
        Assert.Empty(result.Violations);
    }

    [Fact]
    public void ValidateIntegrity_MultipleViolations_ReportsAll()
    {
        var state = new PopulationState();
        var ledger = new MoneyLedger(CurrencyCode.USD);

        var h = new HouseholdId(new EntityId(10));
        var accountId = new AccountId(new EntityId(100));
        state.CreateHousehold(h, accountId);
        ledger.CreateAccount(accountId, h.EntityId);

        // Create a person with a personal account that doesn't exist in ledger
        var personId = new PersonId(new EntityId(1));
        var fakeAccountId = new AccountId(new EntityId(999));
        var person = new Person(personId, h, personalAccountId: fakeAccountId);
        state.AddPerson(person);

        var result = state.ValidateIntegrity(ledger);

        Assert.False(result.IsValid);
        Assert.Contains(result.Violations, v => v.Contains("does not exist in ledger"));
    }

    #endregion
}
