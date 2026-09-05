using CapitalismSim.Simulation.Core.Economics;
using CapitalismSim.Simulation.Core.Entities;
using CapitalismSim.Simulation.Core.Engine;
using CapitalismSim.Simulation.Core.Population;
using CapitalismSim.Simulation.Core.Scheduling;
using CapitalismSim.Simulation.Core.Systems;
using CapitalismSim.Simulation.Core.Time;

namespace CapitalismSim.Simulation.Headless.Scenarios;

/// <summary>
/// Phase 2 demonstration scenario: population with basic needs consumption.
/// Creates households, assigns them initial money, and runs the HouseholdNeedsSystem.
/// </summary>
/// <remarks>
/// This scenario demonstrates:
/// - PopulationState with persons, households, and cohorts
/// - MoneyLedger with account ownership
/// - HouseholdNeedsSystem consuming money from household accounts
/// - Basic needs tracking via HouseholdNeedsState
/// </remarks>
public sealed class Phase2Scenario
{
    public required SimulationEngine Engine { get; init; }
    public required HouseholdNeedsSystem NeedsSystem { get; init; }
    public required CurrencyCode Currency { get; init; }

    public static Phase2Scenario Create(ulong seed, int householdCount = 10, int personsPerHousehold = 4)
    {
        var currency = CurrencyCode.USD;
        var engine = new SimulationEngine(new SimulationEngineOptions
        {
            WorldSeed = seed,
            StartInstant = SimulationInstant.Epoch,
            Currency = currency,
        });

        // Create the HouseholdNeedsSystem with default needs profile
        var needsSystem = new HouseholdNeedsSystem(currency);
        engine.Scheduler.Register(needsSystem, new SchedulingPolicy(
            SimulationInstant.Epoch,
            SimulationDuration.FromDays(1))); // Daily

        // Populate the simulation with households and persons
        PopulateSimulation(engine, householdCount, personsPerHousehold, currency);

        return new Phase2Scenario
        {
            Engine = engine,
            NeedsSystem = needsSystem,
            Currency = currency,
        };
    }

    private static void PopulateSimulation(SimulationEngine engine, int householdCount, int personsPerHousehold, CurrencyCode currency)
    {
        var population = engine.State.Population;
        var ledger = engine.State.MoneyLedger;

        for (int h = 0; h < householdCount; h++)
        {
            // Create household with account
            var householdEntityId = engine.EntityIds.Allocate();
            var householdId = new HouseholdId(householdEntityId);
            var accountEntityId = engine.EntityIds.Allocate();
            var accountId = new AccountId(accountEntityId);

            population.CreateHousehold(householdId, accountId);
            ledger.CreateAccount(accountId, householdEntityId);

            // Initial endowment: $5000 per household
            ledger.ExternalCredit(accountId, new Money(500000, currency), MoneyFlowReason.InitialEndowment);

            // Create persons in household
            for (int p = 0; p < personsPerHousehold; p++)
            {
                var personEntityId = engine.EntityIds.Allocate();
                var personId = new PersonId(personEntityId);

                // Create personal account for the person
                var personalAccountEntityId = engine.EntityIds.Allocate();
                var personalAccountId = new AccountId(personalAccountEntityId);
                ledger.CreateAccount(personalAccountId, personEntityId);

                // Create person (age 18-65 for working adults at epoch)
                int age = 18 + (int)(h * personsPerHousehold + p) % 48;
                var person = Person.WithAge(personId, householdId, age, referenceSimulationDay: 0, personalAccountId);
                population.AddPerson(person);
            }
        }
    }

    /// <summary>
    /// Advances the simulation by the given number of simulated months.
    /// </summary>
    public void AdvanceByMonths(int months)
    {
        Engine.AdvanceBy(SimulationDuration.FromDays(30L * months));
    }

    /// <summary>
    /// Gets a summary of the current simulation state.
    /// </summary>
    public Phase2Summary GetSummary()
    {
        var population = Engine.State.Population;
        var ledger = Engine.State.MoneyLedger;

        long totalBalance = 0;
        int householdsWithNeeds = 0;
        int fullySatisfiedHouseholds = 0;
        int totalPersons = 0;

        foreach (var householdId in population.GetAllHouseholdIds())
        {
            var household = population.GetHousehold(householdId);
            if (household is null) continue;

            var balance = ledger.GetBalance(household.AccountId);
            totalBalance += balance.MinorUnits;
            totalPersons += household.MemberCount;

            var needsState = population.GetHouseholdNeedsState(householdId);
            if (needsState is not null)
            {
                householdsWithNeeds++;
                if (needsState.IsFullySatisfied)
                {
                    fullySatisfiedHouseholds++;
                }
            }
        }

        return new Phase2Summary
        {
            TotalHouseholds = population.HouseholdCount,
            TotalPersons = totalPersons,
            TotalCohorts = 0, // Cohorts are for aggregate LOD, not detailed simulation
            TotalMoneySupply = new Money(totalBalance, Currency),
            AverageHouseholdBalance = population.HouseholdCount > 0
                ? new Money(totalBalance / population.HouseholdCount, Currency)
                : Money.Zero(Currency),
            HouseholdsTracked = householdsWithNeeds,
            FullySatisfiedHouseholds = fullySatisfiedHouseholds,
            SupplyAccountingValid = ledger.VerifySupplyAccounting(),
        };
    }
}

/// <summary>
/// Summary statistics for Phase 2 scenario.
/// </summary>
public sealed record Phase2Summary
{
    public required int TotalHouseholds { get; init; }
    public required int TotalPersons { get; init; }
    public required int TotalCohorts { get; init; }
    public required Money TotalMoneySupply { get; init; }
    public required Money AverageHouseholdBalance { get; init; }
    public required int HouseholdsTracked { get; init; }
    public required int FullySatisfiedHouseholds { get; init; }
    public required bool SupplyAccountingValid { get; init; }
}
