using CapitalismSim.Simulation.Core.Entities;
using CapitalismSim.Simulation.Core.Population;
using Xunit;

namespace CapitalismSim.Simulation.Core.Tests.Population;

public sealed class PersonTests
{
    [Fact]
    public void Constructor_SetsIdAndHouseholdId()
    {
        var personId = new PersonId(new EntityId(1));
        var householdId = new HouseholdId(new EntityId(10));
        var person = new Person(personId, householdId);

        Assert.Equal(personId, person.Id);
        Assert.Equal(householdId, person.HouseholdId);
    }

    [Fact]
    public void None_HasNoneId()
    {
        var none = Person.None;

        Assert.True(none.IsNone);
        Assert.True(none.Id.IsNone);
    }

    [Fact]
    public void Equality_IsValueBased()
    {
        var a = new Person(new PersonId(new EntityId(1)), new HouseholdId(new EntityId(10)));
        var b = new Person(new PersonId(new EntityId(1)), new HouseholdId(new EntityId(10)));
        var c = new Person(new PersonId(new EntityId(2)), new HouseholdId(new EntityId(10)));

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void ToString_ShowsIds()
    {
        var person = new Person(new PersonId(new EntityId(5)), new HouseholdId(new EntityId(20)));

        Assert.Contains("5", person.ToString());
        Assert.Contains("20", person.ToString());
    }
}

public sealed class HouseholdTests
{
    [Fact]
    public void Constructor_SetsIdAndAccountId()
    {
        var householdId = new HouseholdId(new EntityId(1));
        var accountId = new AccountId(new EntityId(100));
        var household = new Household(householdId, accountId);

        Assert.Equal(householdId, household.Id);
        Assert.Equal(accountId, household.AccountId);
        Assert.Empty(household.Members);
    }

    [Fact]
    public void AddMember_AddsPersonToHousehold()
    {
        var household = new Household(new HouseholdId(new EntityId(1)), new AccountId(new EntityId(100)));
        var personId = new PersonId(new EntityId(5));

        household.AddMember(personId);

        Assert.Contains(personId, household.Members);
        Assert.Equal(1, household.MemberCount);
    }

    [Fact]
    public void AddMember_CannotAddSamePersonTwice()
    {
        var household = new Household(new HouseholdId(new EntityId(1)), new AccountId(new EntityId(100)));
        var personId = new PersonId(new EntityId(5));
        household.AddMember(personId);

        Assert.Throws<InvalidOperationException>(() => household.AddMember(personId));
    }

    [Fact]
    public void RemoveMember_RemovesPersonFromHousehold()
    {
        var household = new Household(new HouseholdId(new EntityId(1)), new AccountId(new EntityId(100)));
        var personId = new PersonId(new EntityId(5));
        household.AddMember(personId);

        household.RemoveMember(personId);

        Assert.DoesNotContain(personId, household.Members);
    }

    [Fact]
    public void RemoveMember_NonExistentMember_Throws()
    {
        var household = new Household(new HouseholdId(new EntityId(1)), new AccountId(new EntityId(100)));
        var personId = new PersonId(new EntityId(5));

        Assert.Throws<InvalidOperationException>(() => household.RemoveMember(personId));
    }

    [Fact]
    public void HasMember_ReturnsTrueForMember()
    {
        var household = new Household(new HouseholdId(new EntityId(1)), new AccountId(new EntityId(100)));
        var personId = new PersonId(new EntityId(5));
        household.AddMember(personId);

        Assert.True(household.HasMember(personId));
        Assert.False(household.HasMember(new PersonId(new EntityId(6))));
    }
}

public sealed class PopulationCohortTests
{
    [Fact]
    public void Constructor_SetsIdCountAndHouseholdId()
    {
        var cohortId = new PopulationCohortId(new EntityId(1));
        var householdId = new HouseholdId(new EntityId(10));
        var cohort = new PopulationCohort(cohortId, 500L, householdId);

        Assert.Equal(cohortId, cohort.Id);
        Assert.Equal(500L, cohort.Count);
        Assert.Equal(householdId, cohort.HouseholdId);
    }

    [Fact]
    public void Constructor_NegativeCount_Throws()
    {
        var cohortId = new PopulationCohortId(new EntityId(1));
        var householdId = new HouseholdId(new EntityId(10));

        Assert.Throws<ArgumentOutOfRangeException>(() => new PopulationCohort(cohortId, -1L, householdId));
    }

    [Fact]
    public void Constructor_NoHousehold_AllowsNone()
    {
        var cohortId = new PopulationCohortId(new EntityId(1));
        var cohort = new PopulationCohort(cohortId, 100L, HouseholdId.None);

        Assert.Equal(cohortId, cohort.Id);
        Assert.Equal(100L, cohort.Count);
        Assert.True(cohort.HouseholdId.IsNone);
    }

    [Fact]
    public void Empty_CreatesZeroCountCohort()
    {
        var cohortId = new PopulationCohortId(new EntityId(1));
        var empty = PopulationCohort.Empty(cohortId);

        Assert.Equal(0L, empty.Count);
        Assert.True(empty.IsEmpty);
        Assert.Equal(cohortId, empty.Id);
    }

    [Fact]
    public void WithCount_CreatesNewCohortWithDifferentCount()
    {
        var cohortId = new PopulationCohortId(new EntityId(1));
        var householdId = new HouseholdId(new EntityId(10));
        var original = new PopulationCohort(cohortId, 100L, householdId);

        var updated = original.WithCount(200L);

        Assert.Equal(200L, updated.Count);
        Assert.Equal(householdId, updated.HouseholdId);
        Assert.Equal(cohortId, updated.Id);
        Assert.Equal(100L, original.Count); // Original unchanged
    }

    [Fact]
    public void WithHouseholdId_CreatesNewCohortWithDifferentHousehold()
    {
        var cohortId = new PopulationCohortId(new EntityId(1));
        var householdId1 = new HouseholdId(new EntityId(10));
        var householdId2 = new HouseholdId(new EntityId(20));
        var original = new PopulationCohort(cohortId, 100L, householdId1);

        var updated = original.WithHouseholdId(householdId2);

        Assert.Equal(householdId2, updated.HouseholdId);
        Assert.Equal(100L, updated.Count);
        Assert.Equal(cohortId, updated.Id);
    }

    [Fact]
    public void Addition_MergesCohortsOfSameId()
    {
        var cohortId = new PopulationCohortId(new EntityId(1));
        var householdId = new HouseholdId(new EntityId(10));
        var a = new PopulationCohort(cohortId, 100L, householdId);
        var b = new PopulationCohort(cohortId, 50L, householdId);

        var sum = a + b;

        Assert.Equal(150L, sum.Count);
        Assert.Equal(cohortId, sum.Id);
        Assert.Equal(householdId, sum.HouseholdId);
    }

    [Fact]
    public void Addition_DifferentCohortIds_Throws()
    {
        var id1 = new PopulationCohortId(new EntityId(1));
        var id2 = new PopulationCohortId(new EntityId(2));
        var householdId = new HouseholdId(new EntityId(10));
        var a = new PopulationCohort(id1, 100L, householdId);
        var b = new PopulationCohort(id2, 50L, householdId);

        Assert.Throws<InvalidOperationException>(() => a + b);
    }

    [Fact]
    public void Equality_IsValueBased()
    {
        var cohortId = new PopulationCohortId(new EntityId(1));
        var householdId = new HouseholdId(new EntityId(10));
        var a = new PopulationCohort(cohortId, 100L, householdId);
        var b = new PopulationCohort(cohortId, 100L, householdId);
        var c = new PopulationCohort(cohortId, 200L, householdId);

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.NotEqual(a, c);
    }
}
