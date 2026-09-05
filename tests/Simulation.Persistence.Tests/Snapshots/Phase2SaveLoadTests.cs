using CapitalismSim.Simulation.Core.Economics;
using CapitalismSim.Simulation.Core.Entities;
using CapitalismSim.Simulation.Core.Engine;
using CapitalismSim.Simulation.Core.Population;
using CapitalismSim.Simulation.Core.Time;
using CapitalismSim.Simulation.Persistence.Snapshots;
using Xunit;

namespace CapitalismSim.Simulation.Persistence.Tests.Snapshots;

/// <summary>
/// Phase 2 save/load tests: verify population and money ledger state
/// are correctly preserved across snapshot save/load cycles.
/// </summary>
public sealed class Phase2SaveLoadTests
{
    [Fact]
    public void CaptureThenApply_RestoresPopulationStateExactly()
    {
        // Arrange: create engine with population
        var source = new SimulationEngine(new SimulationEngineOptions
        {
            WorldSeed = 42UL,
            Currency = CurrencyCode.USD,
        });

        var population = source.State.Population;
        var ledger = source.State.MoneyLedger;

        // Create households with accounts
        var household1Id = new HouseholdId(source.EntityIds.Allocate());
        var account1Id = new AccountId(source.EntityIds.Allocate());
        population.CreateHousehold(household1Id, account1Id);
        ledger.CreateAccount(account1Id, household1Id.EntityId);
        ledger.ExternalCredit(account1Id, new Money(500000, CurrencyCode.USD), MoneyFlowReason.InitialEndowment);

        var household2Id = new HouseholdId(source.EntityIds.Allocate());
        var account2Id = new AccountId(source.EntityIds.Allocate());
        population.CreateHousehold(household2Id, account2Id);
        ledger.CreateAccount(account2Id, household2Id.EntityId);
        ledger.ExternalCredit(account2Id, new Money(750000, CurrencyCode.USD), MoneyFlowReason.InitialEndowment);

        // Create persons
        var person1Id = new PersonId(source.EntityIds.Allocate());
        var person1AccountId = new AccountId(source.EntityIds.Allocate());
        ledger.CreateAccount(person1AccountId, person1Id.EntityId);
        var person1 = Person.WithAge(person1Id, household1Id, 25, referenceSimulationDay: 0, person1AccountId);
        population.AddPerson(person1);

        var person2Id = new PersonId(source.EntityIds.Allocate());
        var person2AccountId = new AccountId(source.EntityIds.Allocate());
        ledger.CreateAccount(person2AccountId, person2Id.EntityId);
        var person2 = Person.WithAge(person2Id, household1Id, 30, referenceSimulationDay: 0, person2AccountId);
        population.AddPerson(person2);

        var person3Id = new PersonId(source.EntityIds.Allocate());
        var person3AccountId = new AccountId(source.EntityIds.Allocate());
        ledger.CreateAccount(person3AccountId, person3Id.EntityId);
        var person3 = Person.WithAge(person3Id, household2Id, 45, referenceSimulationDay: 0, person3AccountId);
        population.AddPerson(person3);

        // Act: capture and restore
        KernelSnapshotDto snapshot = KernelSnapshotWriter.Capture(source);

        var target = new SimulationEngine(new SimulationEngineOptions
        {
            WorldSeed = 42UL,
            Currency = CurrencyCode.USD,
        });
        KernelSnapshotReader.Apply(target, snapshot);

        // Assert: population state matches
        Assert.Equal(source.State.Population.IndividualCount, target.State.Population.IndividualCount);
        Assert.Equal(source.State.Population.HouseholdCount, target.State.Population.HouseholdCount);

        // Verify households exist with correct members
        var restoredHousehold1 = target.State.Population.GetHousehold(household1Id);
        Assert.NotNull(restoredHousehold1);
        Assert.Equal(2, restoredHousehold1.MemberCount);
        Assert.True(restoredHousehold1.HasMember(person1Id));
        Assert.True(restoredHousehold1.HasMember(person2Id));

        var restoredHousehold2 = target.State.Population.GetHousehold(household2Id);
        Assert.NotNull(restoredHousehold2);
        Assert.Equal(1, restoredHousehold2.MemberCount);
        Assert.True(restoredHousehold2.HasMember(person3Id));

        // Verify persons exist with correct properties
        var restoredPerson1 = target.State.Population.GetPerson(person1Id);
        Assert.False(restoredPerson1.IsNone);
        Assert.Equal(household1Id, restoredPerson1.HouseholdId);
        Assert.Equal(person1AccountId, restoredPerson1.PersonalAccountId);

        var restoredPerson2 = target.State.Population.GetPerson(person2Id);
        Assert.False(restoredPerson2.IsNone);
        Assert.Equal(household1Id, restoredPerson2.HouseholdId);

        var restoredPerson3 = target.State.Population.GetPerson(person3Id);
        Assert.False(restoredPerson3.IsNone);
        Assert.Equal(household2Id, restoredPerson3.HouseholdId);
    }

    [Fact]
    public void CaptureThenApply_RestoresMoneyLedgerExactly()
    {
        // Arrange: create engine with accounts and transactions
        var source = new SimulationEngine(new SimulationEngineOptions
        {
            WorldSeed = 123UL,
            Currency = CurrencyCode.USD,
        });

        var ledger = source.State.MoneyLedger;

        // Create accounts with different balances
        var account1 = new AccountId(source.EntityIds.Allocate());
        var account2 = new AccountId(source.EntityIds.Allocate());
        var account3 = new AccountId(source.EntityIds.Allocate());

        var owner1 = source.EntityIds.Allocate();
        var owner2 = source.EntityIds.Allocate();
        var owner3 = source.EntityIds.Allocate();

        ledger.CreateAccount(account1, owner1);
        ledger.CreateAccount(account2, owner2);
        ledger.CreateAccount(account3, owner3);

        // External credits
        ledger.ExternalCredit(account1, new Money(100000, CurrencyCode.USD), MoneyFlowReason.InitialEndowment);
        ledger.ExternalCredit(account2, new Money(200000, CurrencyCode.USD), MoneyFlowReason.InitialEndowment);
        ledger.ExternalCredit(account3, new Money(300000, CurrencyCode.USD), MoneyFlowReason.InitialEndowment);

        // Transfers
        ledger.Transfer(account1, account2, new Money(25000, CurrencyCode.USD));
        ledger.Transfer(account3, account1, new Money(50000, CurrencyCode.USD));

        // External debit
        ledger.ExternalDebit(account2, new Money(10000, CurrencyCode.USD), MoneyFlowReason.BasicNeeds);

        // Record expected values before save
        long expectedTotalBalance = ledger.GetTotalBalance().MinorUnits;
        long expectedExternalCredits = ledger.TotalExternalCredits.MinorUnits;
        long expectedExternalDebits = ledger.TotalExternalDebits.MinorUnits;
        long expectedAccount1Balance = ledger.GetBalance(account1).MinorUnits;
        long expectedAccount2Balance = ledger.GetBalance(account2).MinorUnits;
        long expectedAccount3Balance = ledger.GetBalance(account3).MinorUnits;

        // Act: capture and restore
        KernelSnapshotDto snapshot = KernelSnapshotWriter.Capture(source);

        var target = new SimulationEngine(new SimulationEngineOptions
        {
            WorldSeed = 123UL,
            Currency = CurrencyCode.USD,
        });
        KernelSnapshotReader.Apply(target, snapshot);

        // Assert: ledger state matches exactly
        var restoredLedger = target.State.MoneyLedger;

        Assert.Equal(expectedTotalBalance, restoredLedger.GetTotalBalance().MinorUnits);
        Assert.Equal(expectedExternalCredits, restoredLedger.TotalExternalCredits.MinorUnits);
        Assert.Equal(expectedExternalDebits, restoredLedger.TotalExternalDebits.MinorUnits);
        Assert.Equal(expectedAccount1Balance, restoredLedger.GetBalance(account1).MinorUnits);
        Assert.Equal(expectedAccount2Balance, restoredLedger.GetBalance(account2).MinorUnits);
        Assert.Equal(expectedAccount3Balance, restoredLedger.GetBalance(account3).MinorUnits);

        // Verify supply accounting invariant
        Assert.True(restoredLedger.VerifySupplyAccounting());
    }

    [Fact]
    public void CaptureThenApply_RestoresHouseholdNeedsState()
    {
        // Arrange
        var source = new SimulationEngine(new SimulationEngineOptions
        {
            WorldSeed = 999UL,
            Currency = CurrencyCode.USD,
        });

        var population = source.State.Population;
        var ledger = source.State.MoneyLedger;

        var householdId = new HouseholdId(source.EntityIds.Allocate());
        var accountId = new AccountId(source.EntityIds.Allocate());
        population.CreateHousehold(householdId, accountId);
        ledger.CreateAccount(accountId, householdId.EntityId);
        ledger.ExternalCredit(accountId, new Money(100000, CurrencyCode.USD), MoneyFlowReason.InitialEndowment);

        // Create needs state with shortfall history
        var profile = new BasicNeedsProfile(
            new Money(100, CurrencyCode.USD),
            new Money(50, CurrencyCode.USD),
            new Money(25, CurrencyCode.USD));

        var needsState = new HouseholdNeedsState(householdId, profile, memberCount: 3);

        // Simulate some shortfall days
        for (int i = 0; i < 3; i++)
        {
            needsState.RecordSatisfaction(new Money(100, CurrencyCode.USD)); // Partial satisfaction
            needsState.FinalizeDay();
        }

        population.SetHouseholdNeedsState(needsState);

        // Act
        KernelSnapshotDto snapshot = KernelSnapshotWriter.Capture(source);

        var target = new SimulationEngine(new SimulationEngineOptions
        {
            WorldSeed = 999UL,
            Currency = CurrencyCode.USD,
        });
        KernelSnapshotReader.Apply(target, snapshot);

        // Assert
        var restoredNeeds = target.State.Population.GetHouseholdNeedsState(householdId);
        Assert.NotNull(restoredNeeds);
        Assert.Equal(3, restoredNeeds.ConsecutiveShortfallDays);
        Assert.Equal(profile.DailyEssentialCostPerMember.MinorUnits, restoredNeeds.NeedsProfile.DailyEssentialCostPerMember.MinorUnits);
        Assert.Equal(profile.DailyHousingCost.MinorUnits, restoredNeeds.NeedsProfile.DailyHousingCost.MinorUnits);
        Assert.Equal(profile.DailyUtilitiesCost.MinorUnits, restoredNeeds.NeedsProfile.DailyUtilitiesCost.MinorUnits);
    }

    [Fact]
    public void CaptureThenApply_RestoresCohorts()
    {
        // Arrange
        var source = new SimulationEngine(new SimulationEngineOptions { WorldSeed = 555UL });

        var population = source.State.Population;

        // Create cohorts (aggregate population)
        var cohort1Id = new PopulationCohortId(source.EntityIds.Allocate());
        var cohort1 = new PopulationCohort(cohort1Id, count: 1000000L, HouseholdId.None);
        population.SetCohort(cohort1);

        // Create an attached cohort (references an existing household)
        var householdId = new HouseholdId(source.EntityIds.Allocate());
        var accountId = new AccountId(source.EntityIds.Allocate());
        population.CreateHousehold(householdId, accountId);
        source.State.MoneyLedger.CreateAccount(accountId, householdId.EntityId);

        var cohort2Id = new PopulationCohortId(source.EntityIds.Allocate());
        var cohort2 = new PopulationCohort(cohort2Id, count: 500000L, householdId);
        population.SetCohort(cohort2);

        // Act
        KernelSnapshotDto snapshot = KernelSnapshotWriter.Capture(source);

        var target = new SimulationEngine(new SimulationEngineOptions { WorldSeed = 555UL });
        KernelSnapshotReader.Apply(target, snapshot);

        // Assert
        Assert.Equal(2, target.State.Population.CohortCount);

        var restoredCohort1 = target.State.Population.GetCohort(cohort1Id);
        Assert.Equal(1000000L, restoredCohort1.Count);
        Assert.True(restoredCohort1.HouseholdId.IsNone);

        var restoredCohort2 = target.State.Population.GetCohort(cohort2Id);
        Assert.Equal(500000L, restoredCohort2.Count);
        Assert.Equal(householdId, restoredCohort2.HouseholdId);
    }

    [Fact]
    public void SaveLoadEquivalence_Phase2State()
    {
        // This test verifies that a continuous run and a split run (save/load) produce
        // identical Phase 2 state (population, money ledger).

        ulong seed = 12345UL;

        // Continuous run
        var continuousEngine = CreatePhase2Engine(seed);
        continuousEngine.AdvanceBy(SimulationDuration.FromDays(30));

        // Split run: advance 15 days, save, load, continue 15 days
        var firstHalf = CreatePhase2Engine(seed);
        firstHalf.AdvanceBy(SimulationDuration.FromDays(15));

        KernelSnapshotDto snapshot = KernelSnapshotWriter.Capture(firstHalf);

        // Create a fresh engine for restore - population state will be restored from snapshot
        var secondHalf = new SimulationEngine(new SimulationEngineOptions
        {
            WorldSeed = seed,
            Currency = CurrencyCode.USD,
        });
        KernelSnapshotReader.Apply(secondHalf, snapshot);
        secondHalf.AdvanceBy(SimulationDuration.FromDays(15));

        // Assert population state matches
        Assert.Equal(continuousEngine.State.Population.IndividualCount, secondHalf.State.Population.IndividualCount);
        Assert.Equal(continuousEngine.State.Population.HouseholdCount, secondHalf.State.Population.HouseholdCount);

        // Assert money state matches
        Assert.Equal(
            continuousEngine.State.MoneyLedger.GetTotalBalance().MinorUnits,
            secondHalf.State.MoneyLedger.GetTotalBalance().MinorUnits);
        Assert.Equal(
            continuousEngine.State.MoneyLedger.TotalExternalCredits.MinorUnits,
            secondHalf.State.MoneyLedger.TotalExternalCredits.MinorUnits);
        Assert.Equal(
            continuousEngine.State.MoneyLedger.TotalExternalDebits.MinorUnits,
            secondHalf.State.MoneyLedger.TotalExternalDebits.MinorUnits);

        // Verify supply accounting holds in both
        Assert.True(continuousEngine.State.MoneyLedger.VerifySupplyAccounting());
        Assert.True(secondHalf.State.MoneyLedger.VerifySupplyAccounting());
    }

    private static SimulationEngine CreatePhase2Engine(ulong seed)
    {
        var engine = new SimulationEngine(new SimulationEngineOptions
        {
            WorldSeed = seed,
            Currency = CurrencyCode.USD,
        });

        var population = engine.State.Population;
        var ledger = engine.State.MoneyLedger;

        // Create 5 households with 4 persons each
        for (int h = 0; h < 5; h++)
        {
            var householdId = new HouseholdId(engine.EntityIds.Allocate());
            var accountId = new AccountId(engine.EntityIds.Allocate());

            population.CreateHousehold(householdId, accountId);
            ledger.CreateAccount(accountId, householdId.EntityId);
            ledger.ExternalCredit(accountId, new Money(500000, CurrencyCode.USD), MoneyFlowReason.InitialEndowment);

            for (int p = 0; p < 4; p++)
            {
                var personId = new PersonId(engine.EntityIds.Allocate());
                var personalAccountId = new AccountId(engine.EntityIds.Allocate());

                ledger.CreateAccount(personalAccountId, personId.EntityId);

                int age = 18 + (h * 4 + p) % 48;
                var person = Person.WithAge(personId, householdId, age, referenceSimulationDay: 0, personalAccountId);
                population.AddPerson(person);
            }
        }

        return engine;
    }
}
