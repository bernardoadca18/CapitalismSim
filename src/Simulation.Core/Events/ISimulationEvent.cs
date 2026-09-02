using CapitalismSim.Simulation.Core.Time;

namespace CapitalismSim.Simulation.Core.Events;

/// <summary>
/// Marker interface for something that happened in the simulation and may
/// be of interest to other systems (in-process, synchronous consumers —
/// not a message broker; see project brief section 27). No concrete
/// gameplay events (such as a future "EmploymentStarted") are defined in
/// this phase.
/// </summary>
public interface ISimulationEvent
{
}

/// <summary>
/// Wraps a published <see cref="ISimulationEvent"/> with the metadata
/// needed to reason about ordering deterministically: the logical instant
/// it was published at, and a monotonically increasing sequence number
/// that breaks ties between events published at the same instant.
/// </summary>
public sealed record PublishedEvent(
    ISimulationEvent Event,
    SimulationInstant PublishedAt,
    long SequenceNumber);
