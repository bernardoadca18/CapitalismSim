using CapitalismSim.Simulation.Core.Time;

namespace CapitalismSim.Simulation.Core.Scheduling;

/// <summary>
/// Describes how often a registered system should be ticked. This phase
/// supports a single, simple policy — fixed-interval periodic ticking
/// starting from a given instant — which is sufficient to express every
/// cadence mentioned in the project brief (systems ticking every simulated
/// minute, hour, day, and so on).
/// </summary>
public readonly struct SchedulingPolicy
{
    /// <summary>
    /// Creates a periodic policy: the system fires at
    /// <paramref name="firstOccurrence"/>, and then every
    /// <paramref name="interval"/> after that, forever.
    /// </summary>
    public SchedulingPolicy(SimulationInstant firstOccurrence, SimulationDuration interval)
    {
        if (interval.TotalSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(interval),
                interval,
                "A periodic scheduling policy requires a strictly positive interval.");
        }

        FirstOccurrence = firstOccurrence;
        Interval = interval;
    }

    /// <summary>The first instant at which the system should fire.</summary>
    public SimulationInstant FirstOccurrence { get; }

    /// <summary>The fixed interval between subsequent occurrences.</summary>
    public SimulationDuration Interval { get; }

    /// <summary>
    /// Computes the 0-based index of the occurrence that falls exactly at
    /// or most recently before <paramref name="instant"/>, or -1 if
    /// <paramref name="instant"/> is before <see cref="FirstOccurrence"/>.
    /// </summary>
    public long OccurrenceIndexAtOrBefore(SimulationInstant instant)
    {
        if (instant < FirstOccurrence)
        {
            return -1;
        }

        long elapsed = instant.TotalSeconds - FirstOccurrence.TotalSeconds;
        return elapsed / Interval.TotalSeconds;
    }

    /// <summary>Returns the instant at which the given 0-based occurrence index fires.</summary>
    public SimulationInstant InstantOfOccurrence(long occurrenceIndex)
    {
        long offsetSeconds = checked(occurrenceIndex * Interval.TotalSeconds);
        return new SimulationInstant(checked(FirstOccurrence.TotalSeconds + offsetSeconds));
    }
}
