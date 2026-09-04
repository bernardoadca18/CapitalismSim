using CapitalismSim.Simulation.Core.Economics;
using CapitalismSim.Simulation.Core.Entities;
using CapitalismSim.Simulation.Core.Engine;
using CapitalismSim.Simulation.Core.Population;
using CapitalismSim.Simulation.Core.Scheduling;
using CapitalismSim.Simulation.Core.Systems;
using CapitalismSim.Simulation.Core.Time;

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
            SimulationDuration.FromDays(30)));

        // Should not throw
        engine.AdvanceBy(SimulationDuration.FromDays(30));

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
            SimulationDuration.FromDays(30)));

        engine.AdvanceBy(SimulationDuration.FromDays(30));

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
            var person = new Person(personId, householdId, 25 + i, personalAccountId);
            engine.State.Population.AddPerson(person);
        }

        // Give household sufficient funds ($10,000)
        engine.State.MoneyLedger.ExternalCredit(accountId, new Money(1000000, CurrencyCode.USD), MoneyFlowReason.InitialEndowment);

        var profile = new BasicNeedsProfile(
            foodPerPersonMonthly: new Money(30000, CurrencyCode.USD),    // $300 per person
            housingMonthly: new Money(120000, CurrencyCode.USD),         // $1200 rent
            utilitiesMonthly: new Money(15000, CurrencyCode.USD),        // $150 utilities
            healthcarePerPersonMonthly: new Money(20000, CurrencyCode.USD) // $200 per person
        );

        var system = new HouseholdNeedsSystem(profile);
        engine.Scheduler.Register(system, new SchedulingPolicy(
            SimulationInstant.Epoch,
            SimulationDuration.FromDays(30)));

        engine.AdvanceBy(SimulationDuration.FromDays(30));

        // Expected needs: 4 * ($300 + $200) + $1200 + $150 = $2000 + $1350 = $3350
        // Expected balance: $10000 - $3350 = $6650
        var balance = engine.State.MoneyLedger.GetBalance(accountId);
        Assert.Equal(665000, balance.MinorUnits);
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
            var person = new Person(personId, householdId, 25 + i, personalAccountId);
            engine.State.Population.AddPerson(person);
        }

        // Give household insufficient funds ($1000, less than needs)
        engine.State.MoneyLedger.ExternalCredit(accountId, new Money(100000, CurrencyCode.USD), MoneyFlowReason.InitialEndowment);

        var system = new HouseholdNeedsSystem(CurrencyCode.USD);
        engine.Scheduler.Register(system, new SchedulingPolicy(
            SimulationInstant.Epoch,
            SimulationDuration.FromDays(30)));

        engine.AdvanceBy(SimulationDuration.FromDays(30));

        // Balance should be zero (all consumed)
        var balance = engine.State.MoneyLedger.GetBalance(accountId);
        Assert.Equal(0, balance.MinorUnits);
    }

    [Fact]
    public void Tick_RunsMonthly_ConsumesEachMonth()
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
        var person = new Person(personId, householdId, 30, personalAccountId);
        engine.State.Population.AddPerson(person);

        // Give household $10000
        engine.State.MoneyLedger.ExternalCredit(accountId, new Money(1000000, CurrencyCode.USD), MoneyFlowReason.InitialEndowment);

        var profile = new BasicNeedsProfile(
            foodPerPersonMonthly: new Money(10000, CurrencyCode.USD),    // $100
            housingMonthly: new Money(20000, CurrencyCode.USD),         // $200
            utilitiesMonthly: new Money(5000, CurrencyCode.USD),        // $50
            healthcarePerPersonMonthly: new Money(5000, CurrencyCode.USD) // $50
        );

        var system = new HouseholdNeedsSystem(profile);
        engine.Scheduler.Register(system, new SchedulingPolicy(
            SimulationInstant.Epoch,
            SimulationDuration.FromDays(30)));

        // Run for 3 months
        engine.AdvanceBy(SimulationDuration.FromDays(90));

        // Expected needs per month: $100 + $200 + $50 + $50 = $400
        // After 3 months: $10000 - (3 * $400) = $8800
        var balance = engine.State.MoneyLedger.GetBalance(accountId);
        Assert.Equal(880000, balance.MinorUnits);
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
        var person = new Person(personId, householdId, 30, personalAccountId);
        engine.State.Population.AddPerson(person);

        engine.State.MoneyLedger.ExternalCredit(accountId, new Money(100000, CurrencyCode.USD), MoneyFlowReason.InitialEndowment);

        var system = new HouseholdNeedsSystem(CurrencyCode.USD);
        engine.Scheduler.Register(system, new SchedulingPolicy(
            SimulationInstant.Epoch,
            SimulationDuration.FromDays(30)));

        engine.AdvanceBy(SimulationDuration.FromDays(30));

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
        var person = new Person(personId, householdId, 30, personalAccountId);
        engine.State.Population.AddPerson(person);

        engine.State.MoneyLedger.ExternalCredit(accountId, new Money(100000, CurrencyCode.USD), MoneyFlowReason.InitialEndowment);

        var system = new HouseholdNeedsSystem(CurrencyCode.USD);
        engine.Scheduler.Register(system, new SchedulingPolicy(
            SimulationInstant.Epoch,
            SimulationDuration.FromDays(30)));

        engine.AdvanceBy(SimulationDuration.FromDays(30));

        // Check needs state was recorded
        var needsState = engine.State.Population.GetHouseholdNeedsState(householdId);
        Assert.NotNull(needsState);
        Assert.Equal(householdId, needsState.HouseholdId);
    }
}
