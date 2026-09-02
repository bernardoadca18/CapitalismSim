namespace CapitalismSim.Simulation.Persistence.Snapshots;

/// <summary>
/// The serializable representation of a <see cref="Core.Engine.SimulationEngine"/>'s
/// kernel state, sufficient to deterministically resume a simulation that
/// was captured with <see cref="KernelSnapshotWriter"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is intentionally a small, flat schema. It captures only what
/// Phase 1's kernel actually owns: the world seed, the current logical
/// instant, and the entity id allocator's cursor. It does not (and must
/// not) know about any future domain such as people, companies, or
/// property — those phases will extend the snapshot format when they
/// exist, most likely by adding new top-level sections rather than
/// growing this one.
/// </para>
/// </remarks>
public sealed record KernelSnapshotDto
{
    /// <summary>
    /// The schema version of this snapshot. Must match
    /// <see cref="KernelSnapshotWriter.CurrentFormatVersion"/> exactly to be
    /// loaded; see <see cref="SnapshotFormatException"/>.
    /// </summary>
    public required int FormatVersion { get; init; }

    /// <summary>The world seed the simulation was created with.</summary>
    public required ulong WorldSeed { get; init; }

    /// <summary>The logical clock instant at the time of capture, in simulated seconds since epoch.</summary>
    public required long CurrentInstantSeconds { get; init; }

    /// <summary>The next value the entity id allocator will hand out.</summary>
    public required long NextEntityId { get; init; }

    /// <summary>
    /// The next sequence number to be assigned to a published event.
    /// This ensures event sequences remain contiguous across save/load boundaries.
    /// </summary>
    public required long NextEventSequenceNumber { get; init; }

    /// <summary>
    /// Snapshots of the diagnostic probe systems. May be empty if no probe systems
    /// were registered at capture time. The keys are system ids.
    /// </summary>
    public Dictionary<string, ProbeSystemSnapshotDto> ProbeSystems { get; init; } = new();
}

/// <summary>
/// Serializable state of a diagnostic probe system.
/// </summary>
public sealed record ProbeSystemSnapshotDto
{
    /// <summary>The current state of the probe's random stream.</summary>
    public required ulong RandomState { get; init; }

    /// <summary>The increment value for the random stream.</summary>
    public required ulong RandomIncrement { get; init; }

    /// <summary>The running accumulator folded from random draws so far.</summary>
    public required ulong Accumulator { get; init; }
}
