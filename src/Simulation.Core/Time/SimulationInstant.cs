namespace CapitalismSim.Simulation.Core.Time;

/// <summary>
/// Represents a single, absolute point in the simulation's logical timeline,
/// expressed as an integer number of simulated seconds since the simulation
/// epoch (instant zero). This is intentionally not tied to any real-world
/// calendar or wall-clock time.
/// </summary>
/// <remarks>
/// <para>
/// Logical time in the simulation is always represented using integer
/// arithmetic. Floating point time would accumulate rounding error over the
/// long simulated durations this project targets (potentially decades or
/// centuries of simulated time), and would break the determinism guarantees
/// documented in <c>docs/SIMULATION_MODEL.md</c>.
/// </para>
/// <para>
/// This type deliberately does not know about days, months, years, time
/// zones, or civil calendars. A calendar system may be layered on top of it
/// in a future phase without changing this type's contract.
/// </para>
/// </remarks>
public readonly struct SimulationInstant :
    IEquatable<SimulationInstant>,
    IComparable<SimulationInstant>
{
    /// <summary>The instant at simulation epoch (second zero).</summary>
    public static readonly SimulationInstant Epoch = new(0);

    /// <summary>The raw number of simulated seconds since <see cref="Epoch"/>.</summary>
    public long TotalSeconds { get; }

    public SimulationInstant(long totalSeconds)
    {
        TotalSeconds = totalSeconds;
    }

    /// <summary>Adds a duration to this instant, returning a new, later (or earlier) instant.</summary>
    /// <exception cref="OverflowException">The result cannot be represented.</exception>
    public SimulationInstant Add(SimulationDuration duration)
    {
        return new SimulationInstant(checked(TotalSeconds + duration.TotalSeconds));
    }

    /// <summary>Computes the duration between an earlier instant and this instant.</summary>
    /// <exception cref="OverflowException">The result cannot be represented.</exception>
    public SimulationDuration Since(SimulationInstant earlier)
    {
        return new SimulationDuration(checked(TotalSeconds - earlier.TotalSeconds));
    }

    public static SimulationInstant operator +(SimulationInstant instant, SimulationDuration duration)
        => instant.Add(duration);

    public static SimulationDuration operator -(SimulationInstant left, SimulationInstant right)
        => left.Since(right);

    public static SimulationInstant operator -(SimulationInstant instant, SimulationDuration duration)
        => new(checked(instant.TotalSeconds - duration.TotalSeconds));

    public static bool operator ==(SimulationInstant left, SimulationInstant right) => left.Equals(right);

    public static bool operator !=(SimulationInstant left, SimulationInstant right) => !left.Equals(right);

    public static bool operator <(SimulationInstant left, SimulationInstant right) => left.TotalSeconds < right.TotalSeconds;

    public static bool operator <=(SimulationInstant left, SimulationInstant right) => left.TotalSeconds <= right.TotalSeconds;

    public static bool operator >(SimulationInstant left, SimulationInstant right) => left.TotalSeconds > right.TotalSeconds;

    public static bool operator >=(SimulationInstant left, SimulationInstant right) => left.TotalSeconds >= right.TotalSeconds;

    public bool Equals(SimulationInstant other) => TotalSeconds == other.TotalSeconds;

    public override bool Equals(object? obj) => obj is SimulationInstant other && Equals(other);

    public override int GetHashCode() => TotalSeconds.GetHashCode();

    public int CompareTo(SimulationInstant other) => TotalSeconds.CompareTo(other.TotalSeconds);

    public override string ToString() => $"t+{TotalSeconds}s";
}
