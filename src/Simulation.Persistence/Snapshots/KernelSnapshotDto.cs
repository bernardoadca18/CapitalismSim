namespace CapitalismSim.Simulation.Persistence.Snapshots;

/// <summary>
/// Legacy snapshot format that embedded kernel and domain state together.
/// </summary>
/// <remarks>
/// <para>
/// This type is maintained for backward compatibility with existing snapshot files.
/// New code should prefer <see cref="SimulationSnapshotDto"/> which separates kernel
/// state from domain state more cleanly.
/// </para>
/// <para>
/// The type definitions (PopulationSnapshotDto, MoneyLedgerSnapshotDto, etc.)
/// are now defined in SimulationSnapshotDto.cs and are reused here.
/// </para>
/// </remarks>
public sealed record KernelSnapshotDto
{
    /// <summary>
    /// The schema version of this snapshot.
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
    /// </summary>
    public required long NextEventSequenceNumber { get; init; }

    /// <summary>
    /// Snapshot of the population state (Phase 2). May be null for snapshots
    /// created before Phase 2 was implemented.
    /// </summary>
    public PopulationSnapshotDto? Population { get; init; }

    /// <summary>
    /// Snapshot of the monetary ledger state (Phase 2). May be null for snapshots
    /// created before Phase 2 was implemented.
    /// </summary>
    public MoneyLedgerSnapshotDto? MoneyLedger { get; init; }
}
