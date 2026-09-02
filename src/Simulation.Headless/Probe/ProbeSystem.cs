using CapitalismSim.Simulation.Core.Events;
using CapitalismSim.Simulation.Core.Randomness;
using CapitalismSim.Simulation.Core.Scheduling;

namespace CapitalismSim.Simulation.Headless.Probe;

/// <summary>
/// A diagnostic-only scheduled system that exists solely to give the
/// headless runner and tests something concrete to exercise: it draws a
/// deterministic random value on every tick, publishes an event about it,
/// and folds the value into a running accumulator that feeds the
/// deterministic fingerprint.
/// </summary>
/// <remarks>
/// This class intentionally lives in <c>Simulation.Headless</c>, not in
/// <c>Simulation.Core</c>, and must never grow into a stand-in for any real
/// gameplay domain (see project brief section 36).
/// </remarks>
public sealed class ProbeSystem : ISimulationSystem
{
    private readonly SimulationEventBus _events;
    private DeterministicRandom _random;
    private ulong _accumulator;

    public ProbeSystem(SimulationSystemId id, RandomStreamCatalog randomStreams, SimulationEventBus events)
    {
        Id = id;
        _events = events;
        _random = randomStreams.GetStream(new RandomDomain("Probe"), purpose: id.Value);
        _accumulator = 0;
    }

    public SimulationSystemId Id { get; }

    /// <summary>The running accumulator folded from every draw so far.</summary>
    public ulong Accumulator => _accumulator;

    public void Tick(in SimulationTickContext context)
    {
        ulong value = _random.NextUInt64();
        _accumulator = StableHash.Combine(_accumulator, value);
        _events.Publish(new ProbeTickOccurredEvent(context.OccurrenceNumber, value), context.ScheduledInstant);
    }

    /// <summary>
    /// Captures this system's mutable state into a serializable snapshot.
    /// Call this immediately before capturing a kernel snapshot.
    /// </summary>
    public ProbeSystemSnapshot CaptureSnapshot()
    {
        return new ProbeSystemSnapshot
        {
            SystemId = Id.Value,
            RandomState = _random.State,
            RandomIncrement = _random.Increment,
            Accumulator = _accumulator,
        };
    }

    /// <summary>
    /// Restores this system's mutable state from a snapshot.
    /// Call this immediately after a kernel snapshot has been applied to the engine.
    /// If the snapshot's system id does not match, throws <see cref="ArgumentException"/>.
    /// </summary>
    public void RestoreFrom(ProbeSystemSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (snapshot.SystemId != Id.Value)
        {
            throw new ArgumentException(
                $"Snapshot system id '{snapshot.SystemId}' does not match this probe's id '{Id.Value}'.",
                nameof(snapshot));
        }

        _random = DeterministicRandom.FromState(snapshot.RandomState, snapshot.RandomIncrement);
        _accumulator = snapshot.Accumulator;
    }
}
