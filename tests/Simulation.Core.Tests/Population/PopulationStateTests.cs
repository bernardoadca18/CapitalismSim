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
        var person = new Person(personId, householdId);
        state.AddPerson(person);

        Assert.Throws<InvalidOperationException>(() => state.AddPerson(person));
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
        var householdId = new HouseholdId(new EntityId(1));
        var cohort = new PopulationCohort(100L, householdId);

        state.SetCohort(householdId, cohort);

        Assert.Equal(1, state.CohortCount);
        Assert.Equal(cohort, state.GetCohort(householdId));
    }

    [Fact]
    public void SetCohort_MismatchedHouseholdId_Throws()
    {
        var state = new PopulationState();
        var householdId1 = new HouseholdId(new EntityId(1));
        var householdId2 = new HouseholdId(new EntityId(2));
        var cohort = new PopulationCohort(100L, householdId1);

        Assert.Throws<ArgumentException>(() =>
            state.SetCohort(householdId2, cohort));
    }

    [Fact]
    public void GetCohort_NotFound_ReturnsEmpty()
    {
        var state = new PopulationState();
        var householdId = new HouseholdId(new EntityId(1));

        var result = state.GetCohort(householdId);

        Assert.True(result.IsEmpty);
        Assert.Equal(0L, result.Count);
    }

    [Fact]
    public void RemoveCohort_RemovesExistingCohort()
    {
        var state = new PopulationState();
        var householdId = new HouseholdId(new EntityId(1));
        var cohort = new PopulationCohort(100L, householdId);
        state.SetCohort(householdId, cohort);

        var removed = state.RemoveCohort(householdId);

        Assert.True(removed);
        Assert.Equal(0, state.CohortCount);
        Assert.True(state.GetCohort(householdId).IsEmpty);
    }

    [Fact]
    public void RemoveCohort_NonExistent_ReturnsFalse()
    {
        var state = new PopulationState();
        var householdId = new HouseholdId(new EntityId(1));

        var removed = state.RemoveCohort(householdId);

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
        var h2 = new HouseholdId(new EntityId(2));
        state.SetCohort(h2, new PopulationCohort(500L, h2));

        Assert.Equal(503L, state.TotalPopulation);
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
}
