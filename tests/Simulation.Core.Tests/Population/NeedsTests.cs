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
        var essential = new Money(2500L, CurrencyCode.USD);  // $25/day per person
        var housing = new Money(4000L, CurrencyCode.USD);     // $40/day housing
        var utilities = new Money(500L, CurrencyCode.USD);    // $5/day utilities

        var profile = new BasicNeedsProfile(essential, housing, utilities);

        Assert.Equal(essential, profile.DailyEssentialCostPerMember);
        Assert.Equal(housing, profile.DailyHousingCost);
        Assert.Equal(utilities, profile.DailyUtilitiesCost);
    }

    [Fact]
    public void Zero_CreatesZeroCostProfile()
    {
        var profile = BasicNeedsProfile.Zero(CurrencyCode.USD);

        Assert.Equal(Money.Zero(CurrencyCode.USD), profile.DailyEssentialCostPerMember);
        Assert.Equal(Money.Zero(CurrencyCode.USD), profile.DailyHousingCost);
        Assert.Equal(Money.Zero(CurrencyCode.USD), profile.DailyUtilitiesCost);
    }

    [Fact]
    public void CalculateTotalDaily_SumsAllNeeds()
    {
        var profile = new BasicNeedsProfile(
            new Money(2500L, CurrencyCode.USD),  // Essential per person
            new Money(4000L, CurrencyCode.USD),  // Housing
            new Money(500L, CurrencyCode.USD));  // Utilities

        // For 2 people: 2500*2 + 4000 + 500 = 5000 + 4000 + 500 = 9500
        var total = profile.CalculateTotalDaily(2);

        Assert.Equal(new Money(9500L, CurrencyCode.USD), total);
    }

    [Fact]
    public void CalculateTotalDaily_ZeroPeople_StillHasFixedCosts()
    {
        var profile = new BasicNeedsProfile(
            new Money(2500L, CurrencyCode.USD),
            new Money(4000L, CurrencyCode.USD),
            new Money(500L, CurrencyCode.USD));

        // For 0 people: 0 + 4000 + 500 = 4500 (housing and utilities still apply)
        var total = profile.CalculateTotalDaily(0);

        Assert.Equal(new Money(4500L, CurrencyCode.USD), total);
    }

    [Fact]
    public void CalculateTotalDaily_NegativePersonCount_Throws()
    {
        var profile = BasicNeedsProfile.Zero(CurrencyCode.USD);

        Assert.Throws<ArgumentOutOfRangeException>(() => profile.CalculateTotalDaily(-1));
    }

    [Fact]
    public void WithDailyEssentialCostPerMember_CreatesNewProfile()
    {
        var original = new BasicNeedsProfile(
            new Money(2500L, CurrencyCode.USD),
            new Money(4000L, CurrencyCode.USD),
            new Money(500L, CurrencyCode.USD));

        var updated = original.WithDailyEssentialCostPerMember(new Money(3000L, CurrencyCode.USD));

        Assert.Equal(new Money(3000L, CurrencyCode.USD), updated.DailyEssentialCostPerMember);
        Assert.Equal(original.DailyHousingCost, updated.DailyHousingCost);
        Assert.Equal(original.DailyUtilitiesCost, updated.DailyUtilitiesCost);
    }

    [Fact]
    public void WithDailyHousingCost_CreatesNewProfile()
    {
        var original = new BasicNeedsProfile(
            new Money(2500L, CurrencyCode.USD),
            new Money(4000L, CurrencyCode.USD),
            new Money(500L, CurrencyCode.USD));

        var updated = original.WithDailyHousingCost(new Money(5000L, CurrencyCode.USD));

        Assert.Equal(original.DailyEssentialCostPerMember, updated.DailyEssentialCostPerMember);
        Assert.Equal(new Money(5000L, CurrencyCode.USD), updated.DailyHousingCost);
        Assert.Equal(original.DailyUtilitiesCost, updated.DailyUtilitiesCost);
    }

    [Fact]
    public void WithDailyUtilitiesCost_CreatesNewProfile()
    {
        var original = new BasicNeedsProfile(
            new Money(2500L, CurrencyCode.USD),
            new Money(4000L, CurrencyCode.USD),
            new Money(500L, CurrencyCode.USD));

        var updated = original.WithDailyUtilitiesCost(new Money(750L, CurrencyCode.USD));

        Assert.Equal(original.DailyEssentialCostPerMember, updated.DailyEssentialCostPerMember);
        Assert.Equal(original.DailyHousingCost, updated.DailyHousingCost);
        Assert.Equal(new Money(750L, CurrencyCode.USD), updated.DailyUtilitiesCost);
    }

    [Fact]
    public void Equality_IsValueBased()
    {
        var a = new BasicNeedsProfile(
            new Money(2500L, CurrencyCode.USD),
            new Money(4000L, CurrencyCode.USD),
            new Money(500L, CurrencyCode.USD));

        var b = new BasicNeedsProfile(
            new Money(2500L, CurrencyCode.USD),
            new Money(4000L, CurrencyCode.USD),
            new Money(500L, CurrencyCode.USD));

        var c = new BasicNeedsProfile(
            new Money(3000L, CurrencyCode.USD),
            new Money(4000L, CurrencyCode.USD),
            new Money(500L, CurrencyCode.USD));

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
            new Money(2500L, Currency),
            new Money(4000L, Currency),
            new Money(500L, Currency));

        var state = new HouseholdNeedsState(householdId, profile, 2);

        Assert.Equal(householdId, state.HouseholdId);
        Assert.Equal(profile, state.NeedsProfile);
        Assert.Equal(new Money(9500L, Currency), state.TotalNeedsDaily);
        Assert.Equal(Money.Zero(Currency), state.SatisfiedToday);
        Assert.Equal(0, state.ConsecutiveShortfallDays);
        Assert.Equal(Money.Zero(Currency), state.CumulativeShortfall);
    }

    [Fact]
    public void SatisfactionRatio_ZeroWhenNothingSatisfied()
    {
        var householdId = new HouseholdId(new EntityId(1));
        var profile = new BasicNeedsProfile(
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
            Money.Zero(Currency));

        var state = new HouseholdNeedsState(householdId, profile, 1);
        state.RecordSatisfaction(new Money(300L, Currency));
        state.RecordSatisfaction(new Money(200L, Currency));

        Assert.Equal(new Money(500L, Currency), state.SatisfiedToday);
    }

    [Fact]
    public void RecordSatisfaction_WrongCurrency_Throws()
    {
        var householdId = new HouseholdId(new EntityId(1));
        var profile = new BasicNeedsProfile(
            new Money(1000L, Currency),
            Money.Zero(Currency),
            Money.Zero(Currency));

        var state = new HouseholdNeedsState(householdId, profile, 1);

        Assert.Throws<InvalidOperationException>(() =>
            state.RecordSatisfaction(new Money(500L, CurrencyCode.EUR)));
    }

    [Fact]
    public void FinalizeDay_ClearsSatisfiedToday()
    {
        var householdId = new HouseholdId(new EntityId(1));
        var profile = new BasicNeedsProfile(
            new Money(1000L, Currency),
            Money.Zero(Currency),
            Money.Zero(Currency));

        var state = new HouseholdNeedsState(householdId, profile, 1);
        state.RecordSatisfaction(new Money(500L, Currency));
        state.FinalizeDay();

        Assert.Equal(Money.Zero(Currency), state.SatisfiedToday);
    }

    [Fact]
    public void FinalizeDay_WithShortfall_IncrementsConsecutiveDays()
    {
        var householdId = new HouseholdId(new EntityId(1));
        var profile = new BasicNeedsProfile(
            new Money(1000L, Currency),
            Money.Zero(Currency),
            Money.Zero(Currency));

        var state = new HouseholdNeedsState(householdId, profile, 1);
        state.RecordSatisfaction(new Money(500L, Currency));
        state.FinalizeDay();

        Assert.Equal(1, state.ConsecutiveShortfallDays);
        Assert.Equal(new Money(500L, Currency), state.CumulativeShortfall);
    }

    [Fact]
    public void FinalizeDay_WithFullPayment_ResetsConsecutiveDays()
    {
        var householdId = new HouseholdId(new EntityId(1));
        var profile = new BasicNeedsProfile(
            new Money(1000L, Currency),
            Money.Zero(Currency),
            Money.Zero(Currency));

        var state = new HouseholdNeedsState(householdId, profile, 1);

        // Day 1: shortfall
        state.RecordSatisfaction(new Money(500L, Currency));
        state.FinalizeDay();
        Assert.Equal(1, state.ConsecutiveShortfallDays);

        // Day 2: full payment
        state.RecordSatisfaction(new Money(1000L, Currency));
        state.FinalizeDay();

        Assert.Equal(0, state.ConsecutiveShortfallDays);
    }

    [Fact]
    public void FinalizeDay_AccumulatesCumulativeShortfall()
    {
        var householdId = new HouseholdId(new EntityId(1));
        var profile = new BasicNeedsProfile(
            new Money(1000L, Currency),
            Money.Zero(Currency),
            Money.Zero(Currency));

        var state = new HouseholdNeedsState(householdId, profile, 1);

        // Day 1: 500 shortfall
        state.RecordSatisfaction(new Money(500L, Currency));
        state.FinalizeDay();
        Assert.Equal(new Money(500L, Currency), state.CumulativeShortfall);

        // Day 2: 300 shortfall
        state.RecordSatisfaction(new Money(700L, Currency));
        state.FinalizeDay();
        Assert.Equal(new Money(800L, Currency), state.CumulativeShortfall);
    }

    [Fact]
    public void CalculateShortfall_ReturnsUnmetAmount()
    {
        var householdId = new HouseholdId(new EntityId(1));
        var profile = new BasicNeedsProfile(
            new Money(1000L, Currency),
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
            Money.Zero(Currency));

        var state = new HouseholdNeedsState(householdId, originalProfile, 1);

        var newProfile = new BasicNeedsProfile(
            new Money(2000L, Currency),
            Money.Zero(Currency),
            Money.Zero(Currency));

        state.UpdateNeedsProfile(newProfile, 1);

        Assert.Equal(newProfile, state.NeedsProfile);
        Assert.Equal(new Money(2000L, Currency), state.TotalNeedsDaily);
    }

    [Fact]
    public void Clone_CreatesDeepCopy()
    {
        var householdId = new HouseholdId(new EntityId(1));
        var profile = new BasicNeedsProfile(
            new Money(1000L, Currency),
            Money.Zero(Currency),
            Money.Zero(Currency));

        var original = new HouseholdNeedsState(householdId, profile, 1);
        original.RecordSatisfaction(new Money(500L, Currency));
        original.FinalizeDay();

        var clone = original.Clone();

        Assert.Equal(original.HouseholdId, clone.HouseholdId);
        Assert.Equal(original.TotalNeedsDaily, clone.TotalNeedsDaily);
        Assert.Equal(original.SatisfiedToday, clone.SatisfiedToday);
        Assert.Equal(original.ConsecutiveShortfallDays, clone.ConsecutiveShortfallDays);
        Assert.Equal(original.CumulativeShortfall, clone.CumulativeShortfall);
    }
}
