using CapitalismSim.Simulation.Core.Diagnostics;
using CapitalismSim.Simulation.Core.Engine;
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

        return new SimulationRun
        {
            Engine = engine,
            MinuteProbe = minuteProbe,
            HourProbe = hourProbe,
            ProbeEntityHandler = entityHandler,
        };
    }

    /// <summary>Restores kernel state from a snapshot file.</summary>
    public static SimulationRun CreateFromSnapshot(string path, IKernelSnapshotStore store, IDiagnosticSink? diagnosticSink = null)
    {
        KernelSnapshotDto snapshot = store.Load(path);
        SimulationRun run = Create(snapshot.WorldSeed, diagnosticSink);

        KernelSnapshotReader.Apply(run.Engine, snapshot);

        // Note: Probe system state is no longer persisted in the kernel snapshot.
        // The probes start fresh when restored from a snapshot. This is acceptable
        // because probes are diagnostic tools, not core simulation state.

        return run;
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
        return new DeterministicFingerprintBuilder()
            .Add(Engine.WorldSeed)
            .Add(Engine.Clock.Current.TotalSeconds)
            .Add(Engine.EntityIds.NextValue)
            .Add(MinuteProbe.Accumulator)
            .Add(HourProbe.Accumulator)
            .Build();
    }
}
