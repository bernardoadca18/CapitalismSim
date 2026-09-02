using CapitalismSim.Simulation.Core.Events;

namespace CapitalismSim.Simulation.Headless.Probe;

/// <summary>
/// A diagnostic-only event used exclusively to exercise the scheduler, RNG,
/// event bus, and fingerprinting infrastructure end-to-end in the headless
/// runner and in tests. This does not represent, and must never be
/// mistaken for, a real gameplay concept — see project brief section 36.
/// </summary>
public sealed record ProbeTickOccurredEvent(long OccurrenceNumber, ulong DrawnValue) : ISimulationEvent;
