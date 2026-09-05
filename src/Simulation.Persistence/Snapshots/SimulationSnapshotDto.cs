namespace CapitalismSim.Simulation.Persistence.Snapshots;

/// <summary>
/// The top-level serializable representation of a complete simulation state.
/// </summary>
/// <remarks>
/// <para>
/// This is the authoritative save format for the simulation. It composes
/// kernel state with all domain states (population, money, companies, employment, etc.)
/// into a single versioned snapshot.
/// </para>
/// <para>
/// Each domain receives a typed section. Generic extension bags (Dictionary&lt;string, object&gt;)
/// are explicitly forbidden - every real domain must have a typed DTO.
/// </para>
/// <para>
/// <strong>Versioning:</strong> Breaking schema changes must increment <see cref="FormatVersion"/>.
/// We are pre-alpha; backward compatibility with old snapshots is NOT required.
/// Prefer clear <see cref="SnapshotFormatException"/> over complex migrations.
/// </para>
/// </remarks>
public sealed record SimulationSnapshotDto
{
    /// <summary>
    /// The schema version of this snapshot. Must match
    /// <see cref="SimulationSnapshotWriter.CurrentFormatVersion"/> exactly to be loaded.
    /// </summary>
    public required int FormatVersion { get; init; }

    /// <summary>The kernel state (world seed, clock, entity allocator, event bus).</summary>
    public required KernelStateDto Kernel { get; init; }

    /// <summary>The population state (Phase 2+). Null for Phase 1-only snapshots.</summary>
    public PopulationSnapshotDto? Population { get; init; }

    /// <summary>The monetary ledger state (Phase 2+). Null for Phase 1-only snapshots.</summary>
    public MoneyLedgerSnapshotDto? MoneyLedger { get; init; }

    /// <summary>The companies state (Phase 3+). Null for pre-Phase 3 snapshots.</summary>
    public CompaniesSnapshotDto? Companies { get; init; }

    /// <summary>The employment state (Phase 3+). Null for pre-Phase 3 snapshots.</summary>
    public EmploymentSnapshotDto? Employment { get; init; }
}

/// <summary>
/// Kernel-only state. Captures the simulation kernel's core state without domain data.
/// </summary>
public sealed record KernelStateDto
{
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
}

/// <summary>
/// Snapshot of the population state, including individuals, households, and cohorts.
/// </summary>
public sealed record PopulationSnapshotDto
{
    /// <summary>All individuals in the population. Sorted by PersonId for determinism.</summary>
    public required IReadOnlyList<PersonSnapshotDto> Individuals { get; init; }

    /// <summary>All households in the population. Sorted by HouseholdId for determinism.</summary>
    public required IReadOnlyList<HouseholdSnapshotDto> Households { get; init; }

    /// <summary>All population cohorts (aggregated groups for LOD). Sorted by PopulationCohortId.</summary>
    public required IReadOnlyList<CohortSnapshotDto> Cohorts { get; init; }

    /// <summary>Household needs states for tracked households. Sorted by HouseholdId for determinism.</summary>
    public required IReadOnlyList<HouseholdNeedsSnapshotDto> HouseholdNeeds { get; init; }
}

/// <summary>Snapshot of an individual person.</summary>
public sealed record PersonSnapshotDto
{
    /// <summary>The person's entity ID.</summary>
    public required long PersonId { get; init; }

    /// <summary>The household this person belongs to (0 if none).</summary>
    public required long HouseholdId { get; init; }

    /// <summary>The simulation day the person was born (relative to epoch). Negative = born before epoch.</summary>
    public required long BirthDayIndex { get; init; }

    /// <summary>The person's personal account ID (0 if none).</summary>
    public required long PersonalAccountId { get; init; }
}

/// <summary>Snapshot of a household.</summary>
public sealed record HouseholdSnapshotDto
{
    /// <summary>The household's entity ID.</summary>
    public required long HouseholdId { get; init; }

    /// <summary>The monetary account ID for this household.</summary>
    public required long AccountId { get; init; }

    /// <summary>The entity IDs of all members. Sorted by PersonId for determinism.</summary>
    public required IReadOnlyList<long> MemberIds { get; init; }
}

/// <summary>Snapshot of a population cohort (aggregated population group).</summary>
public sealed record CohortSnapshotDto
{
    /// <summary>The cohort's unique entity ID.</summary>
    public required long CohortId { get; init; }

    /// <summary>Number of individuals in this cohort.</summary>
    public required long Count { get; init; }

    /// <summary>The household this cohort belongs to (0 if unattached).</summary>
    public required long HouseholdId { get; init; }
}

/// <summary>Snapshot of household needs state.</summary>
public sealed record HouseholdNeedsSnapshotDto
{
    /// <summary>The household's entity ID.</summary>
    public required long HouseholdId { get; init; }

    /// <summary>Daily essential cost per member (minor units).</summary>
    public required long DailyEssentialCostPerMember { get; init; }

    /// <summary>Daily housing cost (minor units).</summary>
    public required long DailyHousingCost { get; init; }

    /// <summary>Daily utilities cost (minor units).</summary>
    public required long DailyUtilitiesCost { get; init; }

    /// <summary>Number of consecutive days with shortfall.</summary>
    public required int ConsecutiveShortfallDays { get; init; }

    /// <summary>Cumulative shortfall amount (minor units).</summary>
    public required long CumulativeShortfall { get; init; }
}

/// <summary>
/// Snapshot of the monetary ledger state.
/// </summary>
public sealed record MoneyLedgerSnapshotDto
{
    /// <summary>The currency of this ledger (ISO 4217-style 3-letter code).</summary>
    public required string Currency { get; init; }

    /// <summary>All accounts in the ledger. Sorted by AccountId for determinism.</summary>
    public required IReadOnlyList<AccountSnapshotDto> Accounts { get; init; }

    /// <summary>Total money created via ExternalCredit (for supply accounting).</summary>
    public required long TotalExternalCredits { get; init; }

    /// <summary>Total money destroyed via ExternalDebit (for supply accounting).</summary>
    public required long TotalExternalDebits { get; init; }
}

/// <summary>Snapshot of a monetary account.</summary>
public sealed record AccountSnapshotDto
{
    /// <summary>The account's entity ID.</summary>
    public required long AccountId { get; init; }

    /// <summary>The owner entity ID of this account.</summary>
    public required long OwnerId { get; init; }

    /// <summary>The balance in minor units (e.g., cents).</summary>
    public required long BalanceMinorUnits { get; init; }
}

/// <summary>
/// Snapshot of the companies state (Phase 3).
/// </summary>
public sealed record CompaniesSnapshotDto
{
    /// <summary>All companies. Sorted by CompanyId for determinism.</summary>
    public required IReadOnlyList<CompanySnapshotDto> Companies { get; init; }

    /// <summary>All job positions. Sorted by JobPositionId for determinism.</summary>
    public required IReadOnlyList<JobPositionSnapshotDto> JobPositions { get; init; }
}

/// <summary>Snapshot of a company.</summary>
public sealed record CompanySnapshotDto
{
    /// <summary>The company's entity ID.</summary>
    public required long CompanyId { get; init; }

    /// <summary>The company's name.</summary>
    public required string Name { get; init; }

    /// <summary>The company's treasury account ID.</summary>
    public required long TreasuryAccountId { get; init; }
}

/// <summary>Snapshot of a job position.</summary>
public sealed record JobPositionSnapshotDto
{
    /// <summary>The position's entity ID.</summary>
    public required long JobPositionId { get; init; }

    /// <summary>The company that owns this position.</summary>
    public required long CompanyId { get; init; }

    /// <summary>The job role code (e.g., "general-worker", "manager").</summary>
    public required string JobRoleCode { get; init; }

    /// <summary>The pay amount per period (minor units).</summary>
    public required long PayAmountMinorUnits { get; init; }

    /// <summary>The pay interval in days.</summary>
    public required int PayIntervalDays { get; init; }
}

/// <summary>
/// Snapshot of the employment state (Phase 3).
/// </summary>
public sealed record EmploymentSnapshotDto
{
    /// <summary>All employment contracts. Sorted by EmploymentContractId for determinism.</summary>
    public required IReadOnlyList<EmploymentContractSnapshotDto> Contracts { get; init; }

    /// <summary>All payroll states. Sorted by EmploymentContractId for determinism.</summary>
    public required IReadOnlyList<PayrollStateSnapshotDto> PayrollStates { get; init; }
}

/// <summary>Snapshot of an employment contract.</summary>
public sealed record EmploymentContractSnapshotDto
{
    /// <summary>The contract's entity ID.</summary>
    public required long EmploymentContractId { get; init; }

    /// <summary>The person employed under this contract.</summary>
    public required long PersonId { get; init; }

    /// <summary>The company that hired this person.</summary>
    public required long CompanyId { get; init; }

    /// <summary>The job position this contract occupies.</summary>
    public required long JobPositionId { get; init; }

    /// <summary>The simulation day the contract started.</summary>
    public required long StartDayIndex { get; init; }

    /// <summary>The simulation day the contract ended (0 if still active).</summary>
    public required long EndDayIndex { get; init; }

    /// <summary>The pay amount per period (minor units).</summary>
    public required long PayAmountMinorUnits { get; init; }

    /// <summary>The pay interval in days.</summary>
    public required int PayIntervalDays { get; init; }
}

/// <summary>Snapshot of payroll state for a contract.</summary>
public sealed record PayrollStateSnapshotDto
{
    /// <summary>The contract this payroll state belongs to.</summary>
    public required long EmploymentContractId { get; init; }

    /// <summary>Accrued unpaid wages (minor units).</summary>
    public required long AccruedUnpaidWagesMinorUnits { get; init; }

    /// <summary>Number of consecutive missed pay periods.</summary>
    public required int ConsecutiveMissedPayPeriods { get; init; }

    /// <summary>Next pay day index (when payment is due).</summary>
    public required long NextPayDayIndex { get; init; }
}
