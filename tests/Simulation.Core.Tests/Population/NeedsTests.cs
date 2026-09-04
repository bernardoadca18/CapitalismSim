using CapitalismSim.Simulation.Core.Economics;
using CapitalismSim.Simulation.Core.Entities;
using CapitalismSim.Simulation.Core.Population;
using Xunit;

namespace CapitalismSim.Simulation.Core.Tests.Population;

public sealed class BasicNeedsProfileTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var food = new Money(500L, CurrencyCode.USD);
        var housing = new Money(1200L, CurrencyCode.USD);
        var utilities = new Money(150L, CurrencyCode.USD);
        var healthcare = new Money(300L, CurrencyCode.USD);

        var profile = new BasicNeedsProfile(food, housing, utilities, healthcare);

        Assert.Equal(food, profile.FoodPerPersonMonthly);
        Assert.Equal(housing, profile.HousingMonthly);
        Assert.Equal(utilities, profile.UtilitiesMonthly);
        Assert.Equal(healthcare, profile.HealthcarePerPersonMonthly);
    }

    [Fact]
    public void Zero_CreatesZeroCostProfile()
    {
        var profile = BasicNeedsProfile.Zero(CurrencyCode.USD);

        Assert.Equal(Money.Zero(CurrencyCode.USD), profile.FoodPerPersonMonthly);
        Assert.Equal(Money.Zero(CurrencyCode.USD), profile.HousingMonthly);
        Assert.Equal(Money.Zero(CurrencyCode.USD), profile.UtilitiesMonthly);
        Assert.Equal(Money.Zero(CurrencyCode.USD), profile.HealthcarePerPersonMonthly);
    }

    [Fact]
    public void CalculateTotalMonthly_SumsAllNeeds()
    {
        var profile = new BasicNeedsProfile(
            new Money(500L, CurrencyCode.USD),  // Food per person
            new Money(1200L, CurrencyCode.USD), // Housing
            new Money(150L, CurrencyCode.USD),  // Utilities
            new Money(300L, CurrencyCode.USD)); // Healthcare per person

        // For 2 people: 500*2 + 1200 + 150 + 300*2 = 1000 + 1200 + 150 + 600 = 2950
        var total = profile.CalculateTotalMonthly(2);

        Assert.Equal(new Money(2950L, CurrencyCode.USD), total);
    }

    [Fact]
    public void CalculateTotalMonthly_ZeroPeople_StillHasFixedCosts()
    {
        var profile = new BasicNeedsProfile(
            new Money(500L, CurrencyCode.USD),
            new Money(1200L, CurrencyCode.USD),
            new Money(150L, CurrencyCode.USD),
            new Money(300L, CurrencyCode.USD));

        // For 0 people: 0 + 1200 + 150 + 0 = 1350 (housing and utilities still apply)
        var total = profile.CalculateTotalMonthly(0);

        Assert.Equal(new Money(1350L, CurrencyCode.USD), total);
    }

    [Fact]
    public void CalculateTotalMonthly_NegativePersonCount_Throws()
    {
        var profile = BasicNeedsProfile.Zero(CurrencyCode.USD);

        Assert.Throws<ArgumentOutOfRangeException>(() => profile.CalculateTotalMonthly(-1));
    }

    [Fact]
    public void WithFoodPerPersonMonthly_CreatesNewProfile()
    {
        var original = new BasicNeedsProfile(
            new Money(500L, CurrencyCode.USD),
            new Money(1200L, CurrencyCode.USD),
            new Money(150L, CurrencyCode.USD),
            new Money(300L, CurrencyCode.USD));

        var updated = original.WithFoodPerPersonMonthly(new Money(600L, CurrencyCode.USD));

        Assert.Equal(new Money(600L, CurrencyCode.USD), updated.FoodPerPersonMonthly);
        Assert.Equal(original.HousingMonthly, updated.HousingMonthly);
        Assert.Equal(original.UtilitiesMonthly, updated.UtilitiesMonthly);
        Assert.Equal(original.HealthcarePerPersonMonthly, updated.HealthcarePerPersonMonthly);
    }

    [Fact]
    public void Equality_IsValueBased()
    {
        var a = new BasicNeedsProfile(
            new Money(500L, CurrencyCode.USD),
            new Money(1200L, CurrencyCode.USD),
            new Money(150L, CurrencyCode.USD),
            new Money(300L, CurrencyCode.USD));

        var b = new BasicNeedsProfile(
            new Money(500L, CurrencyCode.USD),
            new Money(1200L, CurrencyCode.USD),
            new Money(150L, CurrencyCode.USD),
            new Money(300L, CurrencyCode.USD));

        var c = new BasicNeedsProfile(
            new Money(600L, CurrencyCode.USD),
            new Money(1200L, CurrencyCode.USD),
            new Money(150L, CurrencyCode.USD),
            new Money(300L, CurrencyCode.USD));

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.NotEqual(a, c);
    }
}

public sealed class HouseholdNeedsStateTests
{
    private static readonly CurrencyCode Currency = CurrencyCode.USD;

    [Fact]
    public void Constructor_InitializesWithZeroSatisfaction()
    {
        var householdId = new HouseholdId(new EntityId(1));
        var profile = new BasicNeedsProfile(
            new Money(500L, Currency),
            new Money(1200L, Currency),
            new Money(150L, Currency),
            new Money(300L, Currency));

        var state = new HouseholdNeedsState(householdId, profile, 2);

        Assert.Equal(householdId, state.HouseholdId);
        Assert.Equal(profile, state.NeedsProfile);
        Assert.Equal(new Money(2950L, Currency), state.TotalNeedsMonthly);
        Assert.Equal(Money.Zero(Currency), state.SatisfiedNeedsMonthly);
    }

    [Fact]
    public void SatisfactionRatio_ZeroWhenNothingSatisfied()
    {
        var householdId = new HouseholdId(new EntityId(1));
        var profile = new BasicNeedsProfile(
            new Money(500L, Currency),
            new Money(1000L, Currency),
            Money.Zero(Currency),
            Money.Zero(Currency));

        var state = new HouseholdNeedsState(householdId, profile, 1);

        Assert.Equal(0.0, state.SatisfactionRatio);
    }

    [Fact]
    public void SatisfactionRatio_CalculatesCorrectly()
    {
        var householdId = new HouseholdId(new EntityId(1));
        var profile = new BasicNeedsProfile(
            new Money(1000L, Currency),
            Money.Zero(Currency),
            Money.Zero(Currency),
            Money.Zero(Currency));

        var state = new HouseholdNeedsState(householdId, profile, 1);
        state.RecordSatisfaction(new Money(500L, Currency));

        Assert.Equal(0.5, state.SatisfactionRatio);
    }

    [Fact]
    public void SatisfactionRatio_OneWhenFullySatisfied()
    {
        var householdId = new HouseholdId(new EntityId(1));
        var profile = new BasicNeedsProfile(
            new Money(1000L, Currency),
            Money.Zero(Currency),
            Money.Zero(Currency),
            Money.Zero(Currency));

        var state = new HouseholdNeedsState(householdId, profile, 1);
        state.RecordSatisfaction(new Money(1000L, Currency));

        Assert.Equal(1.0, state.SatisfactionRatio);
    }

    [Fact]
    public void SatisfactionRatio_CanExceedOne()
    {
        var householdId = new HouseholdId(new EntityId(1));
        var profile = new BasicNeedsProfile(
            new Money(1000L, Currency),
            Money.Zero(Currency),
            Money.Zero(Currency),
            Money.Zero(Currency));

        var state = new HouseholdNeedsState(householdId, profile, 1);
        state.RecordSatisfaction(new Money(1500L, Currency));

        Assert.Equal(1.5, state.SatisfactionRatio);
    }

    [Fact]
    public void IsFullySatisfied_TrueWhenNeedsMet()
    {
        var householdId = new HouseholdId(new EntityId(1));
        var profile = new BasicNeedsProfile(
            new Money(1000L, Currency),
            Money.Zero(Currency),
            Money.Zero(Currency),
            Money.Zero(Currency));

        var state = new HouseholdNeedsState(householdId, profile, 1);
        state.RecordSatisfaction(new Money(1000L, Currency));

        Assert.True(state.IsFullySatisfied);
    }

    [Fact]
    public void IsFullySatisfied_TrueWhenExceeded()
    {
        var householdId = new HouseholdId(new EntityId(1));
        var profile = new BasicNeedsProfile(
            new Money(1000L, Currency),
            Money.Zero(Currency),
            Money.Zero(Currency),
            Money.Zero(Currency));

        var state = new HouseholdNeedsState(householdId, profile, 1);
        state.RecordSatisfaction(new Money(1500L, Currency));

        Assert.True(state.IsFullySatisfied);
    }

    [Fact]
    public void RecordSatisfaction_Accumulates()
    {
        var householdId = new HouseholdId(new EntityId(1));
        var profile = new BasicNeedsProfile(
            new Money(1000L, Currency),
            Money.Zero(Currency),
            Money.Zero(Currency),
            Money.Zero(Currency));

        var state = new HouseholdNeedsState(householdId, profile, 1);
        state.RecordSatisfaction(new Money(300L, Currency));
        state.RecordSatisfaction(new Money(200L, Currency));

        Assert.Equal(new Money(500L, Currency), state.SatisfiedNeedsMonthly);
    }

    [Fact]
    public void RecordSatisfaction_WrongCurrency_Throws()
    {
        var householdId = new HouseholdId(new EntityId(1));
        var profile = new BasicNeedsProfile(
            new Money(1000L, Currency),
            Money.Zero(Currency),
            Money.Zero(Currency),
            Money.Zero(Currency));

        var state = new HouseholdNeedsState(householdId, profile, 1);

        Assert.Throws<InvalidOperationException>(() =>
            state.RecordSatisfaction(new Money(500L, CurrencyCode.EUR)));
    }

    [Fact]
    public void ResetForNewPeriod_ClearsSatisfaction()
    {
        var householdId = new HouseholdId(new EntityId(1));
        var profile = new BasicNeedsProfile(
            new Money(1000L, Currency),
            Money.Zero(Currency),
            Money.Zero(Currency),
            Money.Zero(Currency));

        var state = new HouseholdNeedsState(householdId, profile, 1);
        state.RecordSatisfaction(new Money(500L, Currency));
        state.ResetForNewPeriod();

        Assert.Equal(Money.Zero(Currency), state.SatisfiedNeedsMonthly);
    }

    [Fact]
    public void CalculateShortfall_ReturnsUnmetAmount()
    {
        var householdId = new HouseholdId(new EntityId(1));
        var profile = new BasicNeedsProfile(
            new Money(1000L, Currency),
            Money.Zero(Currency),
            Money.Zero(Currency),
            Money.Zero(Currency));

        var state = new HouseholdNeedsState(householdId, profile, 1);
        state.RecordSatisfaction(new Money(300L, Currency));

        var shortfall = state.CalculateShortfall();

        Assert.Equal(new Money(700L, Currency), shortfall);
    }

    [Fact]
    public void CalculateShortfall_ZeroWhenFullySatisfied()
    {
        var householdId = new HouseholdId(new EntityId(1));
        var profile = new BasicNeedsProfile(
            new Money(1000L, Currency),
            Money.Zero(Currency),
            Money.Zero(Currency),
            Money.Zero(Currency));

        var state = new HouseholdNeedsState(householdId, profile, 1);
        state.RecordSatisfaction(new Money(1000L, Currency));

        var shortfall = state.CalculateShortfall();

        Assert.Equal(Money.Zero(Currency), shortfall);
    }

    [Fact]
    public void UpdateNeedsProfile_RecalculatesTotalNeeds()
    {
        var householdId = new HouseholdId(new EntityId(1));
        var originalProfile = new BasicNeedsProfile(
            new Money(1000L, Currency),
            Money.Zero(Currency),
            Money.Zero(Currency),
            Money.Zero(Currency));

        var state = new HouseholdNeedsState(householdId, originalProfile, 1);

        var newProfile = new BasicNeedsProfile(
            new Money(2000L, Currency),
            Money.Zero(Currency),
            Money.Zero(Currency),
            Money.Zero(Currency));

        state.UpdateNeedsProfile(newProfile, 1);

        Assert.Equal(newProfile, state.NeedsProfile);
        Assert.Equal(new Money(2000L, Currency), state.TotalNeedsMonthly);
    }
}
