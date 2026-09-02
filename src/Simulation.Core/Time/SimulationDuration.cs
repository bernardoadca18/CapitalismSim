namespace CapitalismSim.Simulation.Core.Time;

/// <summary>
/// Represents a span of simulated time as an integer number of simulated
/// seconds. Used both for elapsed time between two <see cref="SimulationInstant"/>
/// values and for expressing scheduling cadences (see
/// <see cref="Scheduling.SchedulingPolicy"/>).
/// </summary>
public readonly struct SimulationDuration :
    IEquatable<SimulationDuration>,
    IComparable<SimulationDuration>
{
    public static readonly SimulationDuration Zero = new(0);

    /// <summary>The raw number of simulated seconds this duration represents.</summary>
    public long TotalSeconds { get; }

    public SimulationDuration(long totalSeconds)
    {
        if (totalSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(totalSeconds),
                totalSeconds,
                "A simulation duration cannot be negative.");
        }

        TotalSeconds = totalSeconds;
    }

    public static SimulationDuration FromSeconds(long seconds) => new(seconds);

    public static SimulationDuration FromMinutes(long minutes) => new(checked(minutes * 60));

    public static SimulationDuration FromHours(long hours) => new(checked(hours * 3600));

    public static SimulationDuration FromDays(long days) => new(checked(days * 86_400));

    public SimulationDuration Add(SimulationDuration other)
        => new(checked(TotalSeconds + other.TotalSeconds));

    public static SimulationDuration operator +(SimulationDuration left, SimulationDuration right)
        => left.Add(right);

    public static bool operator ==(SimulationDuration left, SimulationDuration right) => left.Equals(right);

    public static bool operator !=(SimulationDuration left, SimulationDuration right) => !left.Equals(right);

    public static bool operator <(SimulationDuration left, SimulationDuration right) => left.TotalSeconds < right.TotalSeconds;

    public static bool operator <=(SimulationDuration left, SimulationDuration right) => left.TotalSeconds <= right.TotalSeconds;

    public static bool operator >(SimulationDuration left, SimulationDuration right) => left.TotalSeconds > right.TotalSeconds;

    public static bool operator >=(SimulationDuration left, SimulationDuration right) => left.TotalSeconds >= right.TotalSeconds;

    public bool Equals(SimulationDuration other) => TotalSeconds == other.TotalSeconds;

    public override bool Equals(object? obj) => obj is SimulationDuration other && Equals(other);

    public override int GetHashCode() => TotalSeconds.GetHashCode();

    public int CompareTo(SimulationDuration other) => TotalSeconds.CompareTo(other.TotalSeconds);

    public override string ToString() => $"{TotalSeconds}s";
}
