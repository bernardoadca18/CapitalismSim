namespace CapitalismSim.Simulation.Core.Time;

/// <summary>
/// A purely logical clock for the simulation. It never reads wall-clock
/// time and never depends on the host operating system's scheduler; it only
/// advances when explicitly told to by <see cref="SimulationEngine"/>.
/// </summary>
/// <remarks>
/// This type is not thread-safe by design. See
/// docs/adr/0003-deterministic-single-threaded-kernel.md.
/// </remarks>
public sealed class SimulationClock
{
    public SimulationClock(SimulationInstant start)
    {
        Current = start;
    }

    /// <summary>The current logical instant.</summary>
    public SimulationInstant Current { get; private set; }

    /// <summary>
    /// Advances the clock by the given duration. Zero-length advances are
    /// permitted and are a no-op.
    /// </summary>
    public void Advance(SimulationDuration duration)
    {
        Current = Current.Add(duration);
    }

    /// <summary>
    /// Sets the clock to an explicit instant. This must never move the
    /// clock backwards; the simulation timeline is monotonic.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="target"/> is earlier than the current instant.
    /// </exception>
    public void AdvanceTo(SimulationInstant target)
    {
        if (target < Current)
        {
            throw new ArgumentException(
                $"Cannot move the simulation clock backwards. Current={Current}, requested={target}.",
                nameof(target));
        }

        Current = target;
    }

    /// <summary>
    /// Restores the clock to a specific instant, for use only when loading a
    /// snapshot. Unlike <see cref="AdvanceTo"/>, this does not enforce
    /// monotonicity, because a restore is not a "movement in time" from the
    /// caller's perspective — it is establishing the starting point.
    /// </summary>
    public void Restore(SimulationInstant instant)
    {
        Current = instant;
    }
}
