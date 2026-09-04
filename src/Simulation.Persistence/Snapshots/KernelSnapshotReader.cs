using CapitalismSim.Simulation.Core.Economics;
using CapitalismSim.Simulation.Core.Engine;
using CapitalismSim.Simulation.Core.Entities;
using CapitalismSim.Simulation.Core.Population;
using CapitalismSim.Simulation.Core.Time;

namespace CapitalismSim.Simulation.Persistence.Snapshots;

/// <summary>
/// Restores a previously captured <see cref="KernelSnapshotDto"/> onto
/// a freshly constructed <see cref="SimulationEngine"/>.
/// </summary>
/// <remarks>
/// The caller is responsible for constructing a new <see cref="SimulationEngine"/>
/// with the same <see cref="SimulationEngineOptions.WorldSeed"/> as the
/// snapshot before calling <see cref="Apply"/> (or simply reuse
/// <see cref="KernelSnapshotDto.WorldSeed"/> when constructing it). This
/// keeps engine construction explicit rather than having the persistence
/// layer reach into engine internals to change immutable configuration.
/// </remarks>
public static class KernelSnapshotReader
{
    /// <summary>
    /// Validates the snapshot's format version and applies its state onto
    /// <paramref name="engine"/>.
    /// </summary>
    /// <exception cref="SnapshotFormatException">
    /// The snapshot's format version is not supported, its world seed
    /// does not match the target engine's world seed, or any snapshot field
    /// contains an invalid value.
    /// </exception>
    public static void Apply(SimulationEngine engine, KernelSnapshotDto snapshot)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(snapshot);

        if (snapshot.FormatVersion != KernelSnapshotWriter.CurrentFormatVersion)
        {
            throw new SnapshotFormatException(
                $"Cannot load snapshot format version {snapshot.FormatVersion}; " +
                $"this build only supports format version {KernelSnapshotWriter.CurrentFormatVersion}.");
        }

        if (snapshot.WorldSeed != engine.WorldSeed)
        {
            throw new SnapshotFormatException(
                $"Snapshot world seed ({snapshot.WorldSeed}) does not match target engine's " +
                $"world seed ({engine.WorldSeed}). Construct engine with snapshot's seed before restoring.");
        }

        if (snapshot.CurrentInstantSeconds < 0)
        {
            throw new SnapshotFormatException(
                $"Snapshot has an invalid CurrentInstantSeconds {snapshot.CurrentInstantSeconds}; " +
                "time cannot be negative.");
        }

        if (snapshot.NextEntityId < 1)
        {
            throw new SnapshotFormatException(
                $"Snapshot has an invalid NextEntityId {snapshot.NextEntityId}; it must be at least 1.");
        }

        if (snapshot.NextEventSequenceNumber < 1)
        {
            throw new SnapshotFormatException(
                $"Snapshot has an invalid NextEventSequenceNumber {snapshot.NextEventSequenceNumber}; it must be at least 1.");
        }

        // Phase 2 snapshot validation (Population and MoneyLedger are optional for backwards compatibility)
        if (snapshot.Population is not null)
        {
            ValidatePopulationSnapshot(snapshot.Population);
        }

        if (snapshot.MoneyLedger is not null)
        {
            ValidateMoneyLedgerSnapshot(snapshot.MoneyLedger);
        }

        // Apply kernel state
        engine.Clock.Restore(new SimulationInstant(snapshot.CurrentInstantSeconds));
        engine.EntityIds.Restore(snapshot.NextEntityId);
        engine.Events.RestoreSequenceNumber(snapshot.NextEventSequenceNumber);

        // Apply Phase 2 state if present
        if (snapshot.Population is not null)
        {
            ApplyPopulation(engine, snapshot.Population);
        }

        if (snapshot.MoneyLedger is not null)
        {
            ApplyMoneyLedger(engine, snapshot.MoneyLedger);
        }
    }

    private static void ApplyPopulation(SimulationEngine engine, PopulationSnapshotDto population)
    {
        var state = engine.State.Population;

        // Create households first (persons reference them)
        foreach (var household in population.Households)
        {
            state.CreateHousehold(
                new HouseholdId(new EntityId(household.HouseholdId)),
                new AccountId(new EntityId(household.AccountId)));
        }

        // Add individuals
        foreach (var person in population.Individuals)
        {
            var p = new Person(
                new PersonId(new EntityId(person.PersonId)),
                new HouseholdId(new EntityId(person.HouseholdId)),
                person.Age,
                person.PersonalAccountId > 0
                    ? new AccountId(new EntityId(person.PersonalAccountId))
                    : AccountId.None);
            state.AddPerson(p);
        }

        // Add cohorts
        foreach (var cohort in population.Cohorts)
        {
            var c = new PopulationCohort(
                new PopulationCohortId(new EntityId(cohort.CohortId)),
                cohort.Count,
                cohort.HouseholdId > 0
                    ? new HouseholdId(new EntityId(cohort.HouseholdId))
                    : HouseholdId.None);
            state.SetCohort(c);
        }
    }

    private static void ApplyMoneyLedger(SimulationEngine engine, MoneyLedgerSnapshotDto moneyLedger)
    {
        var ledger = engine.State.MoneyLedger;

        // Create accounts with owners
        foreach (var account in moneyLedger.Accounts)
        {
            ledger.CreateAccount(
                new AccountId(new EntityId(account.AccountId)),
                new EntityId(account.OwnerId));
        }

        // Apply balances via external credit
        foreach (var account in moneyLedger.Accounts)
        {
            if (account.BalanceMinorUnits > 0)
            {
                ledger.ExternalCredit(
                    new AccountId(new EntityId(account.AccountId)),
                    new Money(account.BalanceMinorUnits, ledger.Currency),
                    MoneyFlowReason.InitialEndowment);
            }
        }
    }

    private static void ValidatePopulationSnapshot(PopulationSnapshotDto population)
    {
        if (population.Individuals is null)
        {
            throw new SnapshotFormatException("Population snapshot has null Individuals collection.");
        }

        if (population.Households is null)
        {
            throw new SnapshotFormatException("Population snapshot has null Households collection.");
        }

        if (population.Cohorts is null)
        {
            throw new SnapshotFormatException("Population snapshot has null Cohorts collection.");
        }

        foreach (var person in population.Individuals)
        {
            if (person.PersonId < 0)
            {
                throw new SnapshotFormatException(
                    $"Person snapshot has invalid PersonId {person.PersonId}; must be non-negative.");
            }

            if (person.Age < 0)
            {
                throw new SnapshotFormatException(
                    $"Person snapshot has invalid Age {person.Age}; must be non-negative.");
            }
        }

        foreach (var household in population.Households)
        {
            if (household.HouseholdId < 0)
            {
                throw new SnapshotFormatException(
                    $"Household snapshot has invalid HouseholdId {household.HouseholdId}; must be non-negative.");
            }

            if (household.AccountId < 0)
            {
                throw new SnapshotFormatException(
                    $"Household snapshot has invalid AccountId {household.AccountId}; must be non-negative.");
            }

            if (household.MemberIds is null)
            {
                throw new SnapshotFormatException(
                    $"Household {household.HouseholdId} snapshot has null MemberIds collection.");
            }

            foreach (var memberId in household.MemberIds)
            {
                if (memberId < 0)
                {
                    throw new SnapshotFormatException(
                        $"Household {household.HouseholdId} snapshot has invalid MemberId {memberId}; must be non-negative.");
                }
            }
        }

        foreach (var cohort in population.Cohorts)
        {
            if (cohort.CohortId < 0)
            {
                throw new SnapshotFormatException(
                    $"Cohort snapshot has invalid CohortId {cohort.CohortId}; must be non-negative.");
            }

            if (cohort.Count < 0)
            {
                throw new SnapshotFormatException(
                    $"Cohort snapshot has invalid Count {cohort.Count}; must be non-negative.");
            }
        }
    }

    private static void ValidateMoneyLedgerSnapshot(MoneyLedgerSnapshotDto moneyLedger)
    {
        if (string.IsNullOrWhiteSpace(moneyLedger.Currency))
        {
            throw new SnapshotFormatException("MoneyLedger snapshot has null or empty Currency.");
        }

        if (moneyLedger.Currency.Length != 3)
        {
            throw new SnapshotFormatException(
                $"MoneyLedger snapshot has invalid Currency '{moneyLedger.Currency}'; must be exactly 3 characters.");
        }

        if (moneyLedger.Accounts is null)
        {
            throw new SnapshotFormatException("MoneyLedger snapshot has null Accounts collection.");
        }

        foreach (var account in moneyLedger.Accounts)
        {
            if (account.AccountId < 0)
            {
                throw new SnapshotFormatException(
                    $"Account snapshot has invalid AccountId {account.AccountId}; must be non-negative.");
            }

            if (account.OwnerId < 0)
            {
                throw new SnapshotFormatException(
                    $"Account snapshot has invalid OwnerId {account.OwnerId}; must be non-negative.");
            }

            if (account.BalanceMinorUnits < 0)
            {
                throw new SnapshotFormatException(
                    $"Account {account.AccountId} snapshot has invalid BalanceMinorUnits {account.BalanceMinorUnits}; must be non-negative.");
            }
        }

        if (moneyLedger.TotalExternalCredits < 0)
        {
            throw new SnapshotFormatException(
                $"MoneyLedger snapshot has invalid TotalExternalCredits {moneyLedger.TotalExternalCredits}; must be non-negative.");
        }

        if (moneyLedger.TotalExternalDebits < 0)
        {
            throw new SnapshotFormatException(
                $"MoneyLedger snapshot has invalid TotalExternalDebits {moneyLedger.TotalExternalDebits}; must be non-negative.");
        }
    }
}
