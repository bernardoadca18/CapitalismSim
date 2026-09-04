using CapitalismSim.Simulation.Core.Time;

namespace CapitalismSim.Simulation.Core.Scheduling;

/// <summary>
/// A unit of simulation behavior that the <see cref="SimulationScheduler"/>
/// invokes on its own cadence. Implementations should be small and focused;
/// this phase intentionally does not define any concrete gameplay systems
/// (those belong to later phases) — only the diagnostic probe system used
/// to exercise this infrastructure.
/// </summary>
public interface ISimulationSystem
{
    /// <summary>A stable identity used for deterministic ordering and diagnostics.</summary>
    SimulationSystemId Id { get; }

    /// <summary>
    /// Invoked by the scheduler exactly once for every scheduled occurrence
    /// that falls within an advanced time range. Implementations must be
    /// deterministic: given the same <paramref name="context"/> contents,
    /// a tick must always have the same observable effect.
    /// </summary>
    void Tick(in SimulationTickContext context);
}

/// <summary>
/// The minimal context passed to a system on each tick. This is
/// intentionally small and explicit rather than a general-purpose service
/// locator (see docs/ARCHITECTURE.md, "no service locator").
/// </summary>
public readonly struct SimulationTickContext
{
    public SimulationTickContext(
        SimulationInstant scheduledInstant,
        long occurrenceNumber,
        State.SimulationState state)
    {
        ScheduledInstant = scheduledInstant;
        OccurrenceNumber = occurrenceNumber;
        State = state;
    }

    /// <summary>The exact logical instant this occurrence was scheduled for.</summary>
    public SimulationInstant ScheduledInstant { get; }

    /// <summary>
    /// A 1-based counter of how many times this specific system has been
    /// ticked so far, including the current call. Useful for diagnostics
    /// and for probe/test systems.
    /// </summary>
    public long OccurrenceNumber { get; }

    /// <summary>
    /// The root aggregate for all simulation domain state.
    /// Systems read from and mutate this state during their ticks.
    /// </summary>
    public State.SimulationState State { get; }
}
