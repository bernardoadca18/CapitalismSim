using CapitalismSim.Simulation.Core.Economics;
using CapitalismSim.Simulation.Core.Entities;
using CapitalismSim.Simulation.Core.Engine;
using CapitalismSim.Simulation.Core.Population;
using CapitalismSim.Simulation.Core.Scheduling;
using CapitalismSim.Simulation.Core.Systems;
using CapitalismSim.Simulation.Core.Time;
using Xunit;

namespace CapitalismSim.Simulation.Core.Tests.Systems;

public class HouseholdNeedsSystemTests
{
    [Fact]
    public void Constructor_WithNeedsProfile_SetsId()
    {
        var profile = BasicNeedsProfile.Zero(CurrencyCode.USD);
        var system = new HouseholdNeedsSystem(profile);

        Assert.Equal(new SimulationSystemId("household-needs"), system.Id);
    }

    [Fact]
    public void Constructor_WithCurrency_CreatesDefaultNeedsProfile()
    {
        var system = new HouseholdNeedsSystem(CurrencyCode.USD);

        Assert.Equal(new SimulationSystemId("household-needs"), system.Id);
    }

    [Fact]
    public void Tick_WithNoHouseholds_DoesNothing()
    {
        var engine = new SimulationEngine(new SimulationEngineOptions
        {
            WorldSeed = 12345,
            Currency = CurrencyCode.USD,
        });

        var system = new HouseholdNeedsSystem(CurrencyCode.USD);
        engine.Scheduler.Register(system, new SchedulingPolicy(
            SimulationInstant.Epoch,
            SimulationDuration.FromDays(1)));

        // Should not throw
        engine.AdvanceBy(SimulationDuration.FromDays(1));

        Assert.Equal(0, engine.State.Population.HouseholdCount);
    }

    [Fact]
    public void Tick_WithHouseholdWithNoMembers_DoesNothing()
    {
        var engine = new SimulationEngine(new SimulationEngineOptions
        {
            WorldSeed = 12345,
            Currency = CurrencyCode.USD,
        });

        // Create household with no members
        var householdId = new HouseholdId(engine.EntityIds.Allocate());
        var accountId = new AccountId(engine.EntityIds.Allocate());
        engine.State.Population.CreateHousehold(householdId, accountId);
        engine.State.MoneyLedger.CreateAccount(accountId, householdId.EntityId);
        engine.State.MoneyLedger.ExternalCredit(accountId, new Money(100000, CurrencyCode.USD), MoneyFlowReason.InitialEndowment);

        var system = new HouseholdNeedsSystem(CurrencyCode.USD);
        engine.Scheduler.Register(system, new SchedulingPolicy(
            SimulationInstant.Epoch,
            SimulationDuration.FromDays(1)));

        engine.AdvanceBy(SimulationDuration.FromDays(1));

        // Balance should remain unchanged (no members = no needs)
        var balance = engine.State.MoneyLedger.GetBalance(accountId);
        Assert.Equal(100000, balance.MinorUnits);
    }

    [Fact]
    public void Tick_WithSufficientFunds_ConsumesNeedsAmount()
    {
        var engine = new SimulationEngine(new SimulationEngineOptions
        {
            WorldSeed = 12345,
            Currency = CurrencyCode.USD,
        });

        // Create household with members
        var householdId = new HouseholdId(engine.EntityIds.Allocate());
        var accountId = new AccountId(engine.EntityIds.Allocate());
        engine.State.Population.CreateHousehold(householdId, accountId);
        engine.State.MoneyLedger.CreateAccount(accountId, householdId.EntityId);

        // Add members
        for (int i = 0; i < 4; i++)
        {
            var personId = new PersonId(engine.EntityIds.Allocate());
            var personalAccountId = new AccountId(engine.EntityIds.Allocate());
            engine.State.MoneyLedger.CreateAccount(personalAccountId, personId.EntityId);
            var person = Person.WithAge(personId, householdId, 25 + i, referenceSimulationDay: 0, personalAccountId);
            engine.State.Population.AddPerson(person);
        }

        // Give household sufficient funds ($10,000)
        engine.State.MoneyLedger.ExternalCredit(accountId, new Money(1000000, CurrencyCode.USD), MoneyFlowReason.InitialEndowment);

        // Daily needs: $75/person + $120 housing + $15 utilities = $435/day
        var profile = new BasicNeedsProfile(
            dailyEssentialCostPerMember: new Money(7500, CurrencyCode.USD),    // $75/day per person
            dailyHousingCost: new Money(12000, CurrencyCode.USD),             // $120/day housing
            dailyUtilitiesCost: new Money(1500, CurrencyCode.USD)             // $15/day utilities
        );

        var system = new HouseholdNeedsSystem(profile);
        engine.Scheduler.Register(system, new SchedulingPolicy(
            SimulationInstant.Epoch,
            SimulationDuration.FromDays(1)));

        engine.AdvanceBy(SimulationDuration.FromDays(1));

        // Expected needs: 4 * $75 + $120 + $15 = $300 + $135 = $435
        // Expected balance: $10000 - $435 = $9565
        var balance = engine.State.MoneyLedger.GetBalance(accountId);
        Assert.Equal(956500, balance.MinorUnits);
    }

    [Fact]
    public void Tick_WithInsufficientFunds_ConsumesAllBalance()
    {
        var engine = new SimulationEngine(new SimulationEngineOptions
        {
            WorldSeed = 12345,
            Currency = CurrencyCode.USD,
        });

        // Create household with members
        var householdId = new HouseholdId(engine.EntityIds.Allocate());
        var accountId = new AccountId(engine.EntityIds.Allocate());
        engine.State.Population.CreateHousehold(householdId, accountId);
        engine.State.MoneyLedger.CreateAccount(accountId, householdId.EntityId);

        // Add 4 members
        for (int i = 0; i < 4; i++)
        {
            var personId = new PersonId(engine.EntityIds.Allocate());
            var personalAccountId = new AccountId(engine.EntityIds.Allocate());
            engine.State.MoneyLedger.CreateAccount(personalAccountId, personId.EntityId);
            var person = Person.WithAge(personId, householdId, 25 + i, referenceSimulationDay: 0, personalAccountId);
            engine.State.Population.AddPerson(person);
        }

        // Give household insufficient funds ($100, less than needs)
        engine.State.MoneyLedger.ExternalCredit(accountId, new Money(10000, CurrencyCode.USD), MoneyFlowReason.InitialEndowment);

        var system = new HouseholdNeedsSystem(CurrencyCode.USD);
        engine.Scheduler.Register(system, new SchedulingPolicy(
            SimulationInstant.Epoch,
            SimulationDuration.FromDays(1)));

        engine.AdvanceBy(SimulationDuration.FromDays(1));

        // Balance should be zero (all consumed)
        var balance = engine.State.MoneyLedger.GetBalance(accountId);
        Assert.Equal(0, balance.MinorUnits);
    }

    [Fact]
    public void Tick_RunsDaily_ConsumesEachDay()
    {
        var engine = new SimulationEngine(new SimulationEngineOptions
        {
            WorldSeed = 12345,
            Currency = CurrencyCode.USD,
        });

        // Create household with 1 member for simpler calculation
        var householdId = new HouseholdId(engine.EntityIds.Allocate());
        var accountId = new AccountId(engine.EntityIds.Allocate());
        engine.State.Population.CreateHousehold(householdId, accountId);
        engine.State.MoneyLedger.CreateAccount(accountId, householdId.EntityId);

        var personId = new PersonId(engine.EntityIds.Allocate());
        var personalAccountId = new AccountId(engine.EntityIds.Allocate());
        engine.State.MoneyLedger.CreateAccount(personalAccountId, personId.EntityId);
        var person = Person.WithAge(personId, householdId, 30, referenceSimulationDay: 0, personalAccountId);
        engine.State.Population.AddPerson(person);

        // Give household $10000
        engine.State.MoneyLedger.ExternalCredit(accountId, new Money(1000000, CurrencyCode.USD), MoneyFlowReason.InitialEndowment);

        // Daily needs: $100/person + $50 housing + $10 utilities = $160/day
        var profile = new BasicNeedsProfile(
            dailyEssentialCostPerMember: new Money(10000, CurrencyCode.USD),    // $100/day per person
            dailyHousingCost: new Money(5000, CurrencyCode.USD),                // $50/day housing
            dailyUtilitiesCost: new Money(1000, CurrencyCode.USD)                // $10/day utilities
        );

        var system = new HouseholdNeedsSystem(profile);
        engine.Scheduler.Register(system, new SchedulingPolicy(
            SimulationInstant.Epoch,
            SimulationDuration.FromDays(1)));

        // Run for 3 days
        engine.AdvanceBy(SimulationDuration.FromDays(3));

        // Expected needs per day: $100 + $50 + $10 = $160
        // After 3 days: $10000 - (3 * $160) = $9520
        var balance = engine.State.MoneyLedger.GetBalance(accountId);
        Assert.Equal(952000, balance.MinorUnits);
    }

    [Fact]
    public void Tick_MaintainsSupplyAccounting()
    {
        var engine = new SimulationEngine(new SimulationEngineOptions
        {
            WorldSeed = 12345,
            Currency = CurrencyCode.USD,
        });

        // Create household
        var householdId = new HouseholdId(engine.EntityIds.Allocate());
        var accountId = new AccountId(engine.EntityIds.Allocate());
        engine.State.Population.CreateHousehold(householdId, accountId);
        engine.State.MoneyLedger.CreateAccount(accountId, householdId.EntityId);

        var personId = new PersonId(engine.EntityIds.Allocate());
        var personalAccountId = new AccountId(engine.EntityIds.Allocate());
        engine.State.MoneyLedger.CreateAccount(personalAccountId, personId.EntityId);
        var person = Person.WithAge(personId, householdId, 30, referenceSimulationDay: 0, personalAccountId);
        engine.State.Population.AddPerson(person);

        engine.State.MoneyLedger.ExternalCredit(accountId, new Money(100000, CurrencyCode.USD), MoneyFlowReason.InitialEndowment);

        var system = new HouseholdNeedsSystem(CurrencyCode.USD);
        engine.Scheduler.Register(system, new SchedulingPolicy(
            SimulationInstant.Epoch,
            SimulationDuration.FromDays(1)));

        engine.AdvanceBy(SimulationDuration.FromDays(1));

        // Supply accounting should remain valid
        Assert.True(engine.State.MoneyLedger.VerifySupplyAccounting());
    }

    [Fact]
    public void Tick_RecordsNeedsState()
    {
        var engine = new SimulationEngine(new SimulationEngineOptions
        {
            WorldSeed = 12345,
            Currency = CurrencyCode.USD,
        });

        // Create household
        var householdId = new HouseholdId(engine.EntityIds.Allocate());
        var accountId = new AccountId(engine.EntityIds.Allocate());
        engine.State.Population.CreateHousehold(householdId, accountId);
        engine.State.MoneyLedger.CreateAccount(accountId, householdId.EntityId);

        var personId = new PersonId(engine.EntityIds.Allocate());
        var personalAccountId = new AccountId(engine.EntityIds.Allocate());
        engine.State.MoneyLedger.CreateAccount(personalAccountId, personId.EntityId);
        var person = Person.WithAge(personId, householdId, 30, referenceSimulationDay: 0, personalAccountId);
        engine.State.Population.AddPerson(person);

        engine.State.MoneyLedger.ExternalCredit(accountId, new Money(100000, CurrencyCode.USD), MoneyFlowReason.InitialEndowment);

        var system = new HouseholdNeedsSystem(CurrencyCode.USD);
        engine.Scheduler.Register(system, new SchedulingPolicy(
            SimulationInstant.Epoch,
            SimulationDuration.FromDays(1)));

        engine.AdvanceBy(SimulationDuration.FromDays(1));

        // Check needs state was recorded
        var needsState = engine.State.Population.GetHouseholdNeedsState(householdId);
        Assert.NotNull(needsState);
        Assert.Equal(householdId, needsState.HouseholdId);
    }

    [Fact]
    public void Tick_TracksConsecutiveShortfallDays()
    {
        var engine = new SimulationEngine(new SimulationEngineOptions
        {
            WorldSeed = 12345,
            Currency = CurrencyCode.USD,
        });

        // Create household with 1 member
        var householdId = new HouseholdId(engine.EntityIds.Allocate());
        var accountId = new AccountId(engine.EntityIds.Allocate());
        engine.State.Population.CreateHousehold(householdId, accountId);
        engine.State.MoneyLedger.CreateAccount(accountId, householdId.EntityId);

        var personId = new PersonId(engine.EntityIds.Allocate());
        var personalAccountId = new AccountId(engine.EntityIds.Allocate());
        engine.State.MoneyLedger.CreateAccount(personalAccountId, personId.EntityId);
        var person = Person.WithAge(personId, householdId, 30, referenceSimulationDay: 0, personalAccountId);
        engine.State.Population.AddPerson(person);

        // Give household insufficient funds (will have shortfall each day)
        engine.State.MoneyLedger.ExternalCredit(accountId, new Money(1000, CurrencyCode.USD), MoneyFlowReason.InitialEndowment);

        var system = new HouseholdNeedsSystem(CurrencyCode.USD);
        engine.Scheduler.Register(system, new SchedulingPolicy(
            SimulationInstant.Epoch,
            SimulationDuration.FromDays(1)));

        // Run for 3 days
        engine.AdvanceBy(SimulationDuration.FromDays(3));

        // Should have 3 consecutive shortfall days
        var needsState = engine.State.Population.GetHouseholdNeedsState(householdId);
        Assert.NotNull(needsState);
        Assert.Equal(3, needsState.ConsecutiveShortfallDays);
    }

    [Fact]
    public void Tick_ResetsConsecutiveShortfallOnFullPayment()
    {
        var engine = new SimulationEngine(new SimulationEngineOptions
        {
            WorldSeed = 12345,
            Currency = CurrencyCode.USD,
        });

        // Create household with 1 member
        var householdId = new HouseholdId(engine.EntityIds.Allocate());
        var accountId = new AccountId(engine.EntityIds.Allocate());
        engine.State.Population.CreateHousehold(householdId, accountId);
        engine.State.MoneyLedger.CreateAccount(accountId, householdId.EntityId);

        var personId = new PersonId(engine.EntityIds.Allocate());
        var personalAccountId = new AccountId(engine.EntityIds.Allocate());
        engine.State.MoneyLedger.CreateAccount(personalAccountId, personId.EntityId);
        var person = Person.WithAge(personId, householdId, 30, referenceSimulationDay: 0, personalAccountId);
        engine.State.Population.AddPerson(person);

        // Give household insufficient funds initially
        engine.State.MoneyLedger.ExternalCredit(accountId, new Money(1000, CurrencyCode.USD), MoneyFlowReason.InitialEndowment);

        var system = new HouseholdNeedsSystem(CurrencyCode.USD);
        engine.Scheduler.Register(system, new SchedulingPolicy(
            SimulationInstant.Epoch,
            SimulationDuration.FromDays(1)));

        // Run for 2 days with shortfall
        engine.AdvanceBy(SimulationDuration.FromDays(2));

        var needsState = engine.State.Population.GetHouseholdNeedsState(householdId);
        Assert.NotNull(needsState);
        Assert.Equal(2, needsState.ConsecutiveShortfallDays);

        // Add sufficient funds
        engine.State.MoneyLedger.ExternalCredit(accountId, new Money(100000, CurrencyCode.USD), MoneyFlowReason.InitialEndowment);

        // Run for 1 more day (full payment)
        engine.AdvanceBy(SimulationDuration.FromDays(1));

        // Consecutive shortfall should reset to 0
        Assert.Equal(0, needsState.ConsecutiveShortfallDays);
    }
}
