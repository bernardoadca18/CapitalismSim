using CapitalismSim.Simulation.Core.Scheduling;
using CapitalismSim.Simulation.Core.State;
using CapitalismSim.Simulation.Core.Time;
using Xunit;

namespace CapitalismSim.Simulation.Core.Tests.Scheduling;

public sealed class SimulationSchedulerTests
{
    private static readonly string[] s_expectedFirstSecondThird = ["first", "second", "third"];

    private sealed class RecordingSystem : ISimulationSystem
    {
        private readonly List<SimulationTickContext> _ticks = new();

        public RecordingSystem(string id) => Id = new SimulationSystemId(id);

        public SimulationSystemId Id { get; }

        public List<SimulationTickContext> Ticks => _ticks;

        public void Tick(in SimulationTickContext context) => _ticks.Add(context);
    }

    /// <summary>
    /// Verifies the boundary semantics of RunDueOccurrences:
    /// - previousInstant is EXCLUSIVE: an occurrence at exactly previousInstant does NOT fire
    /// - newInstant is INCLUSIVE: an occurrence at exactly newInstant DOES fire
    /// </summary>
    [Fact]
    public void BoundarySemantics_PreviousInstantExclusive_NewInstantInclusive()
    {
        var scheduler = new SimulationScheduler();
        var system = new RecordingSystem("every-minute");
        // System fires at t=0, t=60, t=120, t=180, ... (starting at Epoch with 60s interval)
        scheduler.Register(system, new SchedulingPolicy(SimulationInstant.Epoch, SimulationDuration.FromMinutes(1)));

        // Advance from t=60 to t=120.
        // t=60 is exactly at previousInstant -> should NOT fire (exclusive bound)
        // t=120 is exactly at newInstant -> should fire exactly once (inclusive bound)
        var clock = new SimulationClock(SimulationInstant.Epoch);
        var state = new SimulationState();
        scheduler.RunDueOccurrences(new SimulationInstant(60), new SimulationInstant(120), clock, state);

        Assert.Single(system.Ticks);
        Assert.Equal(120, system.Ticks[0].ScheduledInstant.TotalSeconds);
        // Occurrence numbers are 1-based: t=0 is occ#1, t=60 is occ#2, t=120 is occ#3
        Assert.Equal(3, system.Ticks[0].OccurrenceNumber);
    }

    /// <summary>
    /// Verifies that an occurrence landing exactly on previousInstant is not
    /// double-counted when an advance is split into two consecutive calls.
    /// The first call's newInstant becomes the second call's previousInstant;
    /// that instant must fire in exactly one of the two calls (the first,
    /// because newInstant is inclusive and previousInstant is exclusive).
    /// </summary>
    [Fact]
    public void BoundarySemantics_SplitAdvanceDoesNotDoubleCountBoundaryInstant()
    {
        var scheduler = new SimulationScheduler();
        var system = new RecordingSystem("every-minute");
        scheduler.Register(system, new SchedulingPolicy(SimulationInstant.Epoch, SimulationDuration.FromMinutes(1)));

        // Split (0, 120] into (0, 60] and (60, 120].
        // t=60 must fire in the first call (inclusive end), not the second (exclusive start).
        var clock = new SimulationClock(SimulationInstant.Epoch);
        var state = new SimulationState();
        long firstBatch = scheduler.RunDueOccurrences(SimulationInstant.Epoch, new SimulationInstant(60), clock, state);
        long secondBatch = scheduler.RunDueOccurrences(new SimulationInstant(60), new SimulationInstant(120), clock, state);

        // First batch (0, 60]: t=0 is excluded (Epoch=previousInstant), t=60 fires -> 1 tick.
        Assert.Equal(1, firstBatch);
        // Second batch (60, 120]: t=60 excluded, only t=120 fires -> 1 tick.
        Assert.Equal(1, secondBatch);
        // Total across the full (0, 120] range: 2 occurrences (t=60, t=120).
        // Note: t=0 at Epoch is NOT included because Epoch is the exclusive start.
        Assert.Equal(2, system.Ticks.Count);
    }

    /// <summary>
    /// Verifies that attempting to register a system after the scheduler has started
    /// executing occurrences throws an InvalidOperationException.
    /// </summary>
    [Fact]
    public void Register_AfterFirstAdvance_ThrowsInvalidOperationException()
    {
        var scheduler = new SimulationScheduler();
        var system1 = new RecordingSystem("system1");
        scheduler.Register(system1, new SchedulingPolicy(SimulationInstant.Epoch, SimulationDuration.FromMinutes(1)));

        // First advance seals registration.
        var clock = new SimulationClock(SimulationInstant.Epoch);
        var state = new SimulationState();
        scheduler.RunDueOccurrences(SimulationInstant.Epoch, new SimulationInstant(60), clock, state);

        // Attempting to register after sealing should throw.
        var system2 = new RecordingSystem("system2");
        var ex = Assert.Throws<InvalidOperationException>(() =>
            scheduler.Register(system2, new SchedulingPolicy(SimulationInstant.Epoch, SimulationDuration.FromMinutes(1))));
        Assert.Contains("Cannot register systems after the scheduler has started", ex.Message);
    }

    [Fact]
    public void SingleSystem_FiresAtCorrectCadence()
    {
        var scheduler = new SimulationScheduler();
        var system = new RecordingSystem("every-minute");
        scheduler.Register(system, new SchedulingPolicy(SimulationInstant.Epoch, SimulationDuration.FromMinutes(1)));

        var clock = new SimulationClock(SimulationInstant.Epoch);
        var state = new SimulationState();
        scheduler.RunDueOccurrences(SimulationInstant.Epoch, new SimulationInstant(300), clock, state); // 5 minutes

        Assert.Equal(5, system.Ticks.Count);
        Assert.Equal(60, system.Ticks[0].ScheduledInstant.TotalSeconds);
        Assert.Equal(300, system.Ticks[4].ScheduledInstant.TotalSeconds);
    }

    [Fact]
    public void MultipleSystems_EachFiresOnItsOwnCadence()
    {
        var scheduler = new SimulationScheduler();
        var perMinute = new RecordingSystem("per-minute");
        var perHour = new RecordingSystem("per-hour");

        scheduler.Register(perMinute, new SchedulingPolicy(SimulationInstant.Epoch, SimulationDuration.FromMinutes(1)));
        scheduler.Register(perHour, new SchedulingPolicy(SimulationInstant.Epoch, SimulationDuration.FromHours(1)));

        var clock = new SimulationClock(SimulationInstant.Epoch);
        var state = new SimulationState();
        scheduler.RunDueOccurrences(SimulationInstant.Epoch, new SimulationInstant((long)SimulationDuration.FromDays(3).TotalSeconds), clock, state);

        Assert.Equal(3 * 24 * 60, perMinute.Ticks.Count);
        Assert.Equal(3 * 24, perHour.Ticks.Count);
    }

    [Fact]
    public void SameInstant_ExecutesInRegistrationOrder()
    {
        var scheduler = new SimulationScheduler();
        var executionOrder = new List<string>();

        var first = new OrderRecordingSystem("first", executionOrder);
        var second = new OrderRecordingSystem("second", executionOrder);
        var third = new OrderRecordingSystem("third", executionOrder);

        // All three fire at the same instants (every 60 seconds from epoch).
        scheduler.Register(first, new SchedulingPolicy(SimulationInstant.Epoch, SimulationDuration.FromMinutes(1)));
        scheduler.Register(second, new SchedulingPolicy(SimulationInstant.Epoch, SimulationDuration.FromMinutes(1)));
        scheduler.Register(third, new SchedulingPolicy(SimulationInstant.Epoch, SimulationDuration.FromMinutes(1)));

        var clock = new SimulationClock(SimulationInstant.Epoch);
        var state = new SimulationState();
        scheduler.RunDueOccurrences(SimulationInstant.Epoch, new SimulationInstant(60), clock, state);

        Assert.Equal(s_expectedFirstSecondThird, executionOrder);
    }

    private sealed class OrderRecordingSystem : ISimulationSystem
    {
        private readonly List<string> _sharedLog;

        public OrderRecordingSystem(string id, List<string> sharedLog)
        {
            Id = new SimulationSystemId(id);
            _sharedLog = sharedLog;
        }

        public SimulationSystemId Id { get; }

        public void Tick(in SimulationTickContext context) => _sharedLog.Add(Id.Value);
    }

    [Fact]
    public void LargeAdvance_ProducesExactExpectedOccurrenceCount_NoLostOrDuplicatedTicks()
    {
        var scheduler = new SimulationScheduler();
        var system = new RecordingSystem("daily");
        scheduler.Register(system, new SchedulingPolicy(SimulationInstant.Epoch, SimulationDuration.FromDays(1)));

        // Simulate 100 years in one huge jump.
        var target = new SimulationInstant((long)SimulationDuration.FromDays(365 * 100).TotalSeconds);
        var clock = new SimulationClock(SimulationInstant.Epoch);
        var state = new SimulationState();
        scheduler.RunDueOccurrences(SimulationInstant.Epoch, target, clock, state);

        Assert.Equal(365 * 100, system.Ticks.Count);

        // Verify no duplicate occurrence numbers and strictly increasing instants.
        for (int i = 1; i < system.Ticks.Count; i++)
        {
            Assert.True(system.Ticks[i].ScheduledInstant > system.Ticks[i - 1].ScheduledInstant);
            Assert.Equal(system.Ticks[i - 1].OccurrenceNumber + 1, system.Ticks[i].OccurrenceNumber);
        }
    }

    [Fact]
    public void SplittingOneLargeAdvanceIntoManySmallOnes_ProducesSameTotalOccurrences()
    {
        var wholeScheduler = new SimulationScheduler();
        var wholeSystem = new RecordingSystem("whole");
        wholeScheduler.Register(wholeSystem, new SchedulingPolicy(SimulationInstant.Epoch, SimulationDuration.FromHours(1)));

        var target = new SimulationInstant((long)SimulationDuration.FromDays(10).TotalSeconds);
        var wholeClock = new SimulationClock(SimulationInstant.Epoch);
        var wholeState = new SimulationState();
        wholeScheduler.RunDueOccurrences(SimulationInstant.Epoch, target, wholeClock, wholeState);

        var splitScheduler = new SimulationScheduler();
        var splitSystem = new RecordingSystem("split");
        splitScheduler.Register(splitSystem, new SchedulingPolicy(SimulationInstant.Epoch, SimulationDuration.FromHours(1)));

        var splitClock = new SimulationClock(SimulationInstant.Epoch);
        var splitState = new SimulationState();
        SimulationInstant cursor = SimulationInstant.Epoch;
        for (int day = 0; day < 10; day++)
        {
            SimulationInstant next = cursor.Add(SimulationDuration.FromDays(1));
            splitScheduler.RunDueOccurrences(cursor, next, splitClock, splitState);
            cursor = next;
        }

        Assert.Equal(wholeSystem.Ticks.Count, splitSystem.Ticks.Count);
        for (int i = 0; i < wholeSystem.Ticks.Count; i++)
        {
            Assert.Equal(wholeSystem.Ticks[i].ScheduledInstant, splitSystem.Ticks[i].ScheduledInstant);
        }
    }

    [Fact]
    public void ZeroLengthAdvance_ProducesNoTicks_UnlessLandingExactlyOnAnOccurrence()
    {
        var scheduler = new SimulationScheduler();
        var system = new RecordingSystem("system");
        scheduler.Register(system, new SchedulingPolicy(SimulationInstant.Epoch, SimulationDuration.FromMinutes(1)));

        var clock = new SimulationClock(SimulationInstant.Epoch);
        var state = new SimulationState();
        scheduler.RunDueOccurrences(new SimulationInstant(30), new SimulationInstant(30), clock, state);

        Assert.Empty(system.Ticks);
    }

    [Fact]
    public void RegisteringDuplicateId_Throws()
    {
        var scheduler = new SimulationScheduler();
        var systemA = new RecordingSystem("dup");
        var systemB = new RecordingSystem("dup");

        scheduler.Register(systemA, new SchedulingPolicy(SimulationInstant.Epoch, SimulationDuration.FromMinutes(1)));

        Assert.Throws<ArgumentException>(() =>
            scheduler.Register(systemB, new SchedulingPolicy(SimulationInstant.Epoch, SimulationDuration.FromMinutes(1))));
    }

    [Fact]
    public void SchedulingPolicy_ZeroOrNegativeInterval_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SchedulingPolicy(SimulationInstant.Epoch, SimulationDuration.Zero));
    }

    [Fact]
    public void NewInstantBeforePreviousInstant_Throws()
    {
        var scheduler = new SimulationScheduler();
        var clock = new SimulationClock(SimulationInstant.Epoch);
        var state = new SimulationState();
        Assert.Throws<ArgumentException>(() =>
            scheduler.RunDueOccurrences(new SimulationInstant(100), new SimulationInstant(50), clock, state));
    }

    /// <summary>
    /// P1.6: Verifies that when a system.Tick throws, the exception propagates
    /// and the clock is left at the scheduled instant of the failed tick.
    /// </summary>
    [Fact]
    public void TickThrows_ExceptionPropagates_ClockAtFailedInstant()
    {
        var scheduler = new SimulationScheduler();
        var clock = new SimulationClock(SimulationInstant.Epoch);

        var throwingSystem = new ThrowingSystem("thrower", new SimulationInstant(120));
        scheduler.Register(throwingSystem, new SchedulingPolicy(SimulationInstant.Epoch, SimulationDuration.FromMinutes(1)));

        var state = new SimulationState();
        var ex = Assert.Throws<InvalidOperationException>(() =>
            scheduler.RunDueOccurrences(SimulationInstant.Epoch, new SimulationInstant(180), clock, state));

        Assert.Contains("Intentional test failure", ex.Message);
        // Clock should be at the instant of the failed tick (t=120), not the target (t=180)
        Assert.Equal(120, clock.Current.TotalSeconds);
    }

    /// <summary>
    /// P1.6: Verifies that after a Tick throws, the scheduler can still be used
    /// for subsequent advances (no dead internal state).
    /// </summary>
    [Fact]
    public void TickThrows_SubsequentAdvanceStillWorks()
    {
        var scheduler = new SimulationScheduler();
        var clock = new SimulationClock(SimulationInstant.Epoch);

        var throwingSystem = new ThrowingSystem("thrower", new SimulationInstant(120));
        var recordingSystem = new RecordingSystem("recorder");
        scheduler.Register(throwingSystem, new SchedulingPolicy(SimulationInstant.Epoch, SimulationDuration.FromMinutes(2)));
        scheduler.Register(recordingSystem, new SchedulingPolicy(SimulationInstant.Epoch, SimulationDuration.FromMinutes(3)));

        var state = new SimulationState();
        // First advance: thrower throws at t=120
        Assert.Throws<InvalidOperationException>(() =>
            scheduler.RunDueOccurrences(SimulationInstant.Epoch, new SimulationInstant(120), clock, state));

        // Clock is at t=120 (where the exception occurred)
        Assert.Equal(120, clock.Current.TotalSeconds);

        // Second advance: should work normally from where we left off
        // The recorder should fire at t=180 (its first occurrence after Epoch is at 180s = 3min)
        scheduler.RunDueOccurrences(new SimulationInstant(120), new SimulationInstant(240), clock, state);

        // Clock advances to target
        Assert.Equal(240, clock.Current.TotalSeconds);
        // Recorder fired at t=180 (its 1st occurrence) and t=240 (its 2nd occurrence within this window? No - let me recalculate)
        // Actually Epoch + 3min = 180s, then 180s + 3min = 360s which is > 240
        // So only 1 occurrence at t=180
        Assert.Single(recordingSystem.Ticks);
        Assert.Equal(180, recordingSystem.Ticks[0].ScheduledInstant.TotalSeconds);
    }

    /// <summary>
    /// A test system that throws when Tick is called at a specific instant.
    /// </summary>
    private sealed class ThrowingSystem : ISimulationSystem
    {
        private readonly SimulationInstant _throwAt;

        public ThrowingSystem(string id, SimulationInstant throwAt)
        {
            Id = new SimulationSystemId(id);
            _throwAt = throwAt;
        }

        public SimulationSystemId Id { get; }

        public void Tick(in SimulationTickContext context)
        {
            if (context.ScheduledInstant == _throwAt)
            {
                throw new InvalidOperationException("Intentional test failure at scheduled instant.");
            }
        }
    }
}
