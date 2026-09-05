using CapitalismSim.Simulation.Core.Diagnostics;
using CapitalismSim.Simulation.Core.Economics;
using CapitalismSim.Simulation.Core.Engine;
using CapitalismSim.Simulation.Core.Entities;
using CapitalismSim.Simulation.Core.Population;
using CapitalismSim.Simulation.Core.Scheduling;
using CapitalismSim.Simulation.Core.Time;
using CapitalismSim.Simulation.Headless.Probe;
using CapitalismSim.Simulation.Persistence.Snapshots;

namespace CapitalismSim.Simulation.Headless.Cli;

/// <summary>
/// Builds and drives a single simulation run: constructing the engine,
/// registering the diagnostic probe systems, optionally restoring from a
/// snapshot, advancing time, optionally saving a snapshot, and reporting a
/// deterministic fingerprint. Shared by the normal run mode and the
/// <c>--verify-determinism</c> mode so both exercise identical logic.
/// </summary>
public sealed class SimulationRun
{
    public required SimulationEngine Engine { get; init; }

    public required ProbeSystem MinuteProbe { get; init; }

    public required ProbeSystem HourProbe { get; init; }

    public required AllocateProbeEntityCommandHandler ProbeEntityHandler { get; init; }

    public static SimulationRun Create(ulong seed, IDiagnosticSink? diagnosticSink = null)
    {
        var engine = new SimulationEngine(new SimulationEngineOptions
        {
            WorldSeed = seed,
            StartInstant = SimulationInstant.Epoch,
            DiagnosticSink = diagnosticSink,
        });

        var minuteProbe = new ProbeSystem(new SimulationSystemId("probe.minute"), engine.RandomStreams, engine.Events);
        var hourProbe = new ProbeSystem(new SimulationSystemId("probe.hour"), engine.RandomStreams, engine.Events);

        engine.Scheduler.Register(minuteProbe, new SchedulingPolicy(SimulationInstant.Epoch, SimulationDuration.FromMinutes(1)));
        engine.Scheduler.Register(hourProbe, new SchedulingPolicy(SimulationInstant.Epoch, SimulationDuration.FromHours(1)));

        var entityHandler = new AllocateProbeEntityCommandHandler(engine.EntityIds);
        engine.Commands.RegisterHandler<AllocateProbeEntityCommand>(entityHandler);

        // Initialize Phase 2 state: create households with accounts
        PopulateSimulation(engine, householdCount: 10, personsPerHousehold: 4);

        return new SimulationRun
        {
            Engine = engine,
            MinuteProbe = minuteProbe,
            HourProbe = hourProbe,
            ProbeEntityHandler = entityHandler,
        };
    }

    /// <summary>
    /// Populates the simulation with households and persons for Phase 2.
    /// </summary>
    private static void PopulateSimulation(SimulationEngine engine, int householdCount, int personsPerHousehold)
    {
        var population = engine.State.Population;
        var ledger = engine.State.MoneyLedger;
        var currency = ledger.Currency;

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

    /// <summary>Restores kernel state from a snapshot file.</summary>
    public static SimulationRun CreateFromSnapshot(string path, IKernelSnapshotStore store, IDiagnosticSink? diagnosticSink = null)
    {
        KernelSnapshotDto snapshot = store.Load(path);

        // Determine currency from snapshot, default to USD if not present
        var currency = snapshot.MoneyLedger?.Currency != null
            ? new CurrencyCode(snapshot.MoneyLedger.Currency)
            : CurrencyCode.USD;

        // Create a bare engine without default population - state will be restored from snapshot
        var engine = new SimulationEngine(new SimulationEngineOptions
        {
            WorldSeed = snapshot.WorldSeed,
            StartInstant = SimulationInstant.Epoch,
            DiagnosticSink = diagnosticSink,
            Currency = currency,
        });

        // Register probe systems (diagnostic only - not persisted in snapshot)
        var minuteProbe = new ProbeSystem(new SimulationSystemId("probe.minute"), engine.RandomStreams, engine.Events);
        var hourProbe = new ProbeSystem(new SimulationSystemId("probe.hour"), engine.RandomStreams, engine.Events);

        engine.Scheduler.Register(minuteProbe, new SchedulingPolicy(SimulationInstant.Epoch, SimulationDuration.FromMinutes(1)));
        engine.Scheduler.Register(hourProbe, new SchedulingPolicy(SimulationInstant.Epoch, SimulationDuration.FromHours(1)));

        var entityHandler = new AllocateProbeEntityCommandHandler(engine.EntityIds);
        engine.Commands.RegisterHandler<AllocateProbeEntityCommand>(entityHandler);

        // Restore state from snapshot
        KernelSnapshotReader.Apply(engine, snapshot);

        return new SimulationRun
        {
            Engine = engine,
            MinuteProbe = minuteProbe,
            HourProbe = hourProbe,
            ProbeEntityHandler = entityHandler,
        };
    }

    /// <summary>Advances the simulation by the given number of simulated days, dispatching a probe command once per simulated day.</summary>
    public void AdvanceByDays(int days)
    {
        for (int day = 0; day < days; day++)
        {
            Engine.Commands.Dispatch(new AllocateProbeEntityCommand());
            Engine.AdvanceBy(SimulationDuration.FromDays(1));
        }
    }

    /// <summary>Captures and saves a kernel snapshot.</summary>
    public void SaveTo(string path, IKernelSnapshotStore store)
    {
        KernelSnapshotDto snapshot = KernelSnapshotWriter.Capture(Engine);
        store.Save(path, snapshot);
    }

    /// <summary>
    /// Computes a deterministic fingerprint of the run's kernel state.
    /// Deliberately includes only values that are actually captured and
    /// restored by <see cref="KernelSnapshotDto"/> — never ephemeral,
    /// in-memory-only counters such as <see cref="Core.Diagnostics.SimulationMetrics"/>
    /// or the event bus's publish count, which reset to zero on every new
    /// <see cref="SimulationEngine"/> instance and would otherwise make a
    /// continuous run and a save/load/continue run of the same total
    /// duration report different fingerprints despite being in the same
    /// kernel state.
    /// </summary>
    public string ComputeFingerprint()
    {
        var population = Engine.State.Population;
        var money = Engine.State.MoneyLedger;

        return new DeterministicFingerprintBuilder()
            .Add(Engine.WorldSeed)
            .Add(Engine.Clock.Current.TotalSeconds)
            .Add(Engine.EntityIds.NextValue)
            .Add(MinuteProbe.Accumulator)
            .Add(HourProbe.Accumulator)
            // Phase 2 state
            .Add(population.IndividualCount)
            .Add(population.HouseholdCount)
            .Add(money.GetTotalBalance().MinorUnits)
            .Build();
    }
}
