using CapitalismSim.Simulation.Core.Engine;
using CapitalismSim.Simulation.Core.Events;
using CapitalismSim.Simulation.Core.Randomness;
using CapitalismSim.Simulation.Core.Scheduling;
using CapitalismSim.Simulation.Core.Time;
using CapitalismSim.Simulation.Persistence.Snapshots;
using Xunit;

namespace CapitalismSim.Simulation.Persistence.Tests.Snapshots;

/// <summary>
/// The single most important acceptance test for Phase 1: a continuous run
/// must be observationally indistinguishable from an equivalent run that is
/// interrupted, saved, reloaded, and continued. See project brief sections
/// 41 and 85.
/// </summary>
public sealed class SaveLoadEquivalenceTests
{
    /// <summary>
    /// A minimal scheduled system, local to this test, that draws
    /// deterministic random values and folds them into an accumulator —
    /// exactly the kind of thing a real future gameplay system would do,
    /// used here purely to prove the save/load contract without depending
    /// on Simulation.Headless's diagnostic probe system.
    /// </summary>
    private sealed class AccumulatingSystem : ISimulationSystem
    {
        private DeterministicRandom _random;

        public AccumulatingSystem(RandomStreamCatalog catalog)
        {
            Id = new SimulationSystemId("test.accumulating-system");
            _random = catalog.GetStream(new RandomDomain("Test"));
        }

        public SimulationSystemId Id { get; }

        public ulong Accumulator { get; private set; }

        public void Tick(in SimulationTickContext context)
        {
            Accumulator = StableHash.Combine(Accumulator, _random.NextUInt64());
        }

        public (ulong State, ulong Increment, ulong Accum) SaveState()
        {
            return (_random.State, _random.Increment, Accumulator);
        }

        public void RestoreState(ulong state, ulong increment, ulong accumulator)
        {
            _random = DeterministicRandom.FromState(state, increment);
            Accumulator = accumulator;
        }
    }

    private static (SimulationEngine Engine, AccumulatingSystem System) BuildWorld(ulong seed)
    {
        var engine = new SimulationEngine(new SimulationEngineOptions { WorldSeed = seed });
        var system = new AccumulatingSystem(engine.RandomStreams);
        engine.Scheduler.Register(system, new SchedulingPolicy(SimulationInstant.Epoch, SimulationDuration.FromMinutes(1)));
        return (engine, system);
    }

    private static string Fingerprint(SimulationEngine engine, AccumulatingSystem system)
    {
        return new CapitalismSim.Simulation.Core.Diagnostics.DeterministicFingerprintBuilder()
            .Add(engine.WorldSeed)
            .Add(engine.Clock.Current.TotalSeconds)
            .Add(engine.EntityIds.NextValue)
            .Add(system.Accumulator)
            .Build();
    }

    [Fact]
    public void ContinuousRun_MatchesSaveLoadContinueRun_ForSameTotalDuration()
    {
        const ulong seed = 123UL;

        // Continuous: advance 1000 minutes in one go.
        (SimulationEngine continuousEngine, AccumulatingSystem continuousSystem) = BuildWorld(seed);
        for (int i = 0; i < 10; i++)
        {
            continuousEngine.EntityIds.Allocate();
        }
        continuousEngine.AdvanceBy(SimulationDuration.FromMinutes(1000));
        string continuousFingerprint = Fingerprint(continuousEngine, continuousSystem);

        // Split: advance 500 minutes, save, load into a fresh engine, advance the remaining 500 minutes.
        (SimulationEngine firstHalfEngine, AccumulatingSystem firstHalfSystem) = BuildWorld(seed);
        for (int i = 0; i < 10; i++)
        {
            firstHalfEngine.EntityIds.Allocate();
        }
        firstHalfEngine.AdvanceBy(SimulationDuration.FromMinutes(500));

        (ulong state, ulong increment, ulong accum) = firstHalfSystem.SaveState();
        KernelSnapshotDto snapshot = KernelSnapshotWriter.Capture(firstHalfEngine);

        string path = Path.Combine(Path.GetTempPath(), $"equivalence-{Guid.NewGuid():N}.json");
        var store = new JsonFileKernelSnapshotStore();
        try
        {
            store.Save(path, snapshot);
            KernelSnapshotDto loadedSnapshot = store.Load(path);

            (SimulationEngine secondHalfEngine, AccumulatingSystem secondHalfSystem) = BuildWorld(seed);
            KernelSnapshotReader.Apply(secondHalfEngine, loadedSnapshot);
            secondHalfSystem.RestoreState(state, increment, accum);

            secondHalfEngine.AdvanceBy(SimulationDuration.FromMinutes(500));
            string splitFingerprint = Fingerprint(secondHalfEngine, secondHalfSystem);

            Assert.Equal(continuousFingerprint, splitFingerprint);

            // Also assert the underlying observable state matches component-by-component,
            // not just the combined fingerprint, to make a future regression easy to diagnose.
            Assert.Equal(continuousEngine.Clock.Current, secondHalfEngine.Clock.Current);
            Assert.Equal(continuousEngine.EntityIds.NextValue, secondHalfEngine.EntityIds.NextValue);
            Assert.Equal(continuousSystem.Accumulator, secondHalfSystem.Accumulator);
            Assert.Equal(continuousEngine.Events.NextSequenceNumber, secondHalfEngine.Events.NextSequenceNumber);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Exercises the event bus during the run to verify that the
    /// NextEventSequenceNumber is preserved through a save/load cycle.
    /// Without this, the sequence counter would reset to 1 on reload and
    /// future events would collide with previously-issued sequence numbers.
    /// </summary>
    [Fact]
    public void SaveLoad_PreservesEventSequenceNumber_WhenEventsPublished()
    {
        const ulong seed = 42UL;

        (SimulationEngine engine, AccumulatingSystem system) = BuildWorld(seed);
        engine.AdvanceBy(SimulationDuration.FromMinutes(100));

        // Publish several events to advance the sequence counter.
        for (int i = 0; i < 5; i++)
        {
            engine.Events.Publish(new TestEvent(), engine.Clock.Current);
        }

        long sequenceBeforeSave = engine.Events.NextSequenceNumber;
        Assert.True(sequenceBeforeSave > 1, "Expected sequence number to have advanced past 1.");

        (ulong state, ulong increment, ulong accum) = system.SaveState();
        KernelSnapshotDto snapshot = KernelSnapshotWriter.Capture(engine);

        (SimulationEngine restoredEngine, AccumulatingSystem restoredSystem) = BuildWorld(seed);
        KernelSnapshotReader.Apply(restoredEngine, snapshot);
        restoredSystem.RestoreState(state, increment, accum);

        Assert.Equal(sequenceBeforeSave, restoredEngine.Events.NextSequenceNumber);

        // Publishing after restore should continue from the saved sequence number.
        restoredEngine.Events.Publish(new TestEvent(), restoredEngine.Clock.Current);
        long sequenceAfterPublish = restoredEngine.Events.NextSequenceNumber;
        Assert.Equal(sequenceBeforeSave + 1, sequenceAfterPublish);
    }

    /// <summary>
    /// A minimal event type used only to drive the event bus's sequence
    /// counter in tests.
    /// </summary>
    private sealed record TestEvent : ISimulationEvent;

    [Theory]
    [InlineData(1UL, 30)]
    [InlineData(2UL, 90)]
    [InlineData(999999UL, 7)]
    public void SaveLoadEquivalence_HoldsAcrossVariousSeedsAndDurations(ulong seed, int totalMinutes)
    {
        int splitPoint = totalMinutes / 2;

        (SimulationEngine continuousEngine, AccumulatingSystem continuousSystem) = BuildWorld(seed);
        continuousEngine.AdvanceBy(SimulationDuration.FromMinutes(totalMinutes));

        (SimulationEngine firstHalfEngine, AccumulatingSystem firstHalfSystem) = BuildWorld(seed);
        firstHalfEngine.AdvanceBy(SimulationDuration.FromMinutes(splitPoint));
        (ulong state, ulong increment, ulong accum) = firstHalfSystem.SaveState();
        KernelSnapshotDto snapshot = KernelSnapshotWriter.Capture(firstHalfEngine);

        (SimulationEngine secondHalfEngine, AccumulatingSystem secondHalfSystem) = BuildWorld(seed);
        KernelSnapshotReader.Apply(secondHalfEngine, snapshot);
        secondHalfSystem.RestoreState(state, increment, accum);
        secondHalfEngine.AdvanceBy(SimulationDuration.FromMinutes(totalMinutes - splitPoint));

        Assert.Equal(Fingerprint(continuousEngine, continuousSystem), Fingerprint(secondHalfEngine, secondHalfSystem));
    }
}
