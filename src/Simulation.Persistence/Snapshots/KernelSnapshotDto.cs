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

/// <summary>
/// Snapshot of the population state, including individuals, households, and cohorts.
/// </summary>
public sealed record PopulationSnapshotDto
{
    /// <summary>All individuals in the population.</summary>
    public required IReadOnlyList<PersonSnapshotDto> Individuals { get; init; }

    /// <summary>All households in the population.</summary>
    public required IReadOnlyList<HouseholdSnapshotDto> Households { get; init; }

    /// <summary>All population cohorts (aggregated groups for LOD).</summary>
    public required IReadOnlyList<CohortSnapshotDto> Cohorts { get; init; }
}

/// <summary>Snapshot of an individual person.</summary>
public sealed record PersonSnapshotDto
{
    /// <summary>The person's entity ID.</summary>
    public required long PersonId { get; init; }

    /// <summary>The household this person belongs to (0 if none).</summary>
    public required long HouseholdId { get; init; }
}

/// <summary>Snapshot of a household.</summary>
public sealed record HouseholdSnapshotDto
{
    /// <summary>The household's entity ID.</summary>
    public required long HouseholdId { get; init; }

    /// <summary>The monetary account ID for this household.</summary>
    public required long AccountId { get; init; }

    /// <summary>The entity IDs of all members.</summary>
    public required IReadOnlyList<long> MemberIds { get; init; }
}

/// <summary>Snapshot of a population cohort (aggregated population group).</summary>
public sealed record CohortSnapshotDto
{
    /// <summary>The household this cohort belongs to (0 if unattached).</summary>
    public required long HouseholdId { get; init; }

    /// <summary>Number of individuals in this cohort.</summary>
    public required long Count { get; init; }
}

/// <summary>
/// Snapshot of the monetary ledger state.
/// </summary>
public sealed record MoneyLedgerSnapshotDto
{
    /// <summary>The currency of this ledger (ISO 4217-style 3-letter code).</summary>
    public required string Currency { get; init; }

    /// <summary>All accounts in the ledger.</summary>
    public required IReadOnlyList<AccountSnapshotDto> Accounts { get; init; }
}

/// <summary>Snapshot of a monetary account.</summary>
public sealed record AccountSnapshotDto
{
    /// <summary>The account's entity ID.</summary>
    public required long AccountId { get; init; }

    /// <summary>The balance in minor units (e.g., cents).</summary>
    public required long BalanceMinorUnits { get; init; }
}
