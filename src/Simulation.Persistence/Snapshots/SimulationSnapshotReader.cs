using CapitalismSim.Simulation.Core.Companies;
using CapitalismSim.Simulation.Core.Economics;
using CapitalismSim.Simulation.Core.Engine;
using CapitalismSim.Simulation.Core.Entities;
using CapitalismSim.Simulation.Core.Employment;
using CapitalismSim.Simulation.Core.Population;
using CapitalismSim.Simulation.Core.Time;

namespace CapitalismSim.Simulation.Persistence.Snapshots;

/// <summary>
/// Restores a previously captured <see cref="SimulationSnapshotDto"/> onto
/// a freshly constructed <see cref="SimulationEngine"/>.
/// </summary>
/// <remarks>
/// Validates all invariants and integrity before mutating any state.
/// </remarks>
public static class SimulationSnapshotReader
{
    /// <summary>
    /// Validates the snapshot's format version and applies its state onto
    /// <paramref name="engine"/>.
    /// </summary>
    public static void Apply(SimulationEngine engine, SimulationSnapshotDto snapshot)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(snapshot);

        ValidateFormatVersion(snapshot.FormatVersion);
        ValidateKernelSnapshot(snapshot.Kernel);

        // 1. Global entity id uniqueness and allocator cursor validation
        ValidateGlobalEntitiesAndCursor(snapshot);

        // 2. Domain-level structural validations
        if (snapshot.Population is not null)
        {
            ValidatePopulationSnapshot(snapshot.Population);
        }

        if (snapshot.MoneyLedger is not null)
        {
            ValidateMoneyLedgerSnapshot(snapshot.MoneyLedger);
        }

        if (snapshot.Companies is not null)
        {
            ValidateCompaniesSnapshot(snapshot.Companies);
        }

        if (snapshot.Employment is not null)
        {
            ValidateEmploymentSnapshot(snapshot.Employment);
        }

        // 3. Cross-domain reconciliation and ownership validations
        ValidateCrossReferencesAndOwnership(snapshot);

        // All validations passed! Now apply kernel and domain state.
        ApplyKernel(engine, snapshot.Kernel);

        if (snapshot.Population is not null)
        {
            ApplyPopulation(engine, snapshot.Population);
        }

        if (snapshot.MoneyLedger is not null)
        {
            ApplyMoneyLedger(engine, snapshot.MoneyLedger);
        }

        if (snapshot.Companies is not null)
        {
            ApplyCompanies(engine, snapshot.Companies);
        }

        if (snapshot.Employment is not null)
        {
            ApplyEmployment(engine, snapshot.Employment);
        }
    }

    private static void ValidateFormatVersion(int formatVersion)
    {
        if (formatVersion != SimulationSnapshotWriter.CurrentFormatVersion)
        {
            throw new SnapshotFormatException(
                $"Cannot load snapshot format version {formatVersion}; " +
                $"this build only supports format version {SimulationSnapshotWriter.CurrentFormatVersion}.");
        }
    }

    private static void ValidateKernelSnapshot(KernelStateDto kernel)
    {
        if (kernel.CurrentInstantSeconds < 0)
        {
            throw new SnapshotFormatException(
                $"Snapshot has an invalid CurrentInstantSeconds {kernel.CurrentInstantSeconds}; " +
                "time cannot be negative.");
        }

        if (kernel.NextEntityId < 1)
        {
            throw new SnapshotFormatException(
                $"Snapshot has an invalid NextEntityId {kernel.NextEntityId}; it must be at least 1.");
        }

        if (kernel.NextEventSequenceNumber < 1)
        {
            throw new SnapshotFormatException(
                $"Snapshot has an invalid NextEventSequenceNumber {kernel.NextEventSequenceNumber}; it must be at least 1.");
        }
    }

    private static void ValidateGlobalEntitiesAndCursor(SimulationSnapshotDto snapshot)
    {
        var globalEntities = new HashSet<long>();
        long maxDeclaredEntityId = 0;

        void RegisterDeclaredId(long id, string kind)
        {
            if (id <= 0)
            {
                throw new SnapshotFormatException(
                    $"{kind} has invalid entity ID {id}; real entity IDs must be strictly positive (> 0).");
            }

            if (!globalEntities.Add(id))
            {
                throw new SnapshotFormatException(
                    $"Entity ID collision detected for ID {id} ({kind}); duplicate or shared entity declaration across domains.");
            }

            if (id > maxDeclaredEntityId)
            {
                maxDeclaredEntityId = id;
            }
        }

        if (snapshot.Population is not null)
        {
            if (snapshot.Population.Individuals is not null)
            {
                foreach (var p in snapshot.Population.Individuals)
                {
                    RegisterDeclaredId(p.PersonId, "Person");
                }
            }

            if (snapshot.Population.Households is not null)
            {
                foreach (var h in snapshot.Population.Households)
                {
                    RegisterDeclaredId(h.HouseholdId, "Household");
                }
            }

            if (snapshot.Population.Cohorts is not null)
            {
                foreach (var c in snapshot.Population.Cohorts)
                {
                    RegisterDeclaredId(c.CohortId, "Cohort");
                }
            }
        }

        if (snapshot.MoneyLedger is not null && snapshot.MoneyLedger.Accounts is not null)
        {
            foreach (var a in snapshot.MoneyLedger.Accounts)
            {
                RegisterDeclaredId(a.AccountId, "Account");
            }
        }

        if (snapshot.Companies is not null)
        {
            if (snapshot.Companies.Companies is not null)
            {
                foreach (var c in snapshot.Companies.Companies)
                {
                    RegisterDeclaredId(c.CompanyId, "Company");
                }
            }

            if (snapshot.Companies.JobPositions is not null)
            {
                foreach (var p in snapshot.Companies.JobPositions)
                {
                    RegisterDeclaredId(p.JobPositionId, "JobPosition");
                }
            }
        }

        if (snapshot.Employment is not null && snapshot.Employment.Contracts is not null)
        {
            foreach (var c in snapshot.Employment.Contracts)
            {
                RegisterDeclaredId(c.EmploymentContractId, "EmploymentContract");
            }
        }

        if (snapshot.Kernel.NextEntityId <= maxDeclaredEntityId)
        {
            throw new SnapshotFormatException(
                $"Kernel NextEntityId ({snapshot.Kernel.NextEntityId}) must be strictly greater than max declared EntityId ({maxDeclaredEntityId}).");
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
            if (person.PersonId <= 0)
            {
                throw new SnapshotFormatException(
                    $"Person snapshot has invalid PersonId {person.PersonId}; must be positive (> 0).");
            }

            if (person.HouseholdId < 0)
            {
                throw new SnapshotFormatException(
                    $"Person {person.PersonId} has invalid HouseholdId {person.HouseholdId}; must be non-negative (0 = none).");
            }

            if (person.PersonalAccountId < 0)
            {
                throw new SnapshotFormatException(
                    $"Person {person.PersonId} has invalid PersonalAccountId {person.PersonalAccountId}; must be non-negative (0 = none).");
            }
        }

        foreach (var household in population.Households)
        {
            if (household.HouseholdId <= 0)
            {
                throw new SnapshotFormatException(
                    $"Household snapshot has invalid HouseholdId {household.HouseholdId}; must be positive (> 0).");
            }

            if (household.AccountId <= 0)
            {
                throw new SnapshotFormatException(
                    $"Household {household.HouseholdId} snapshot has invalid AccountId {household.AccountId}; must be positive (> 0).");
            }

            if (household.MemberIds is null)
            {
                throw new SnapshotFormatException(
                    $"Household {household.HouseholdId} snapshot has null MemberIds collection.");
            }

            foreach (var memberId in household.MemberIds)
            {
                if (memberId <= 0)
                {
                    throw new SnapshotFormatException(
                        $"Household {household.HouseholdId} snapshot has invalid MemberId {memberId}; must be positive (> 0).");
                }
            }
        }

        foreach (var cohort in population.Cohorts)
        {
            if (cohort.CohortId <= 0)
            {
                throw new SnapshotFormatException(
                    $"Cohort snapshot has invalid CohortId {cohort.CohortId}; must be positive (> 0).");
            }

            if (cohort.Count < 0)
            {
                throw new SnapshotFormatException(
                    $"Cohort {cohort.CohortId} snapshot has invalid Count {cohort.Count}; must be non-negative.");
            }

            if (cohort.HouseholdId < 0)
            {
                throw new SnapshotFormatException(
                    $"Cohort {cohort.CohortId} snapshot has invalid HouseholdId {cohort.HouseholdId}; must be non-negative (0 = unattached).");
            }
        }

        if (population.HouseholdNeeds is not null)
        {
            var seenNeedsHouseholds = new HashSet<long>();
            foreach (var needs in population.HouseholdNeeds)
            {
                if (needs.HouseholdId <= 0)
                {
                    throw new SnapshotFormatException(
                        $"HouseholdNeeds snapshot has invalid HouseholdId {needs.HouseholdId}; must be positive (> 0).");
                }

                if (!seenNeedsHouseholds.Add(needs.HouseholdId))
                {
                    throw new SnapshotFormatException(
                        $"Duplicate HouseholdNeeds state detected for HouseholdId {needs.HouseholdId}.");
                }

                if (needs.DailyEssentialCostPerMember < 0)
                {
                    throw new SnapshotFormatException(
                        $"HouseholdNeeds {needs.HouseholdId} has invalid DailyEssentialCostPerMember {needs.DailyEssentialCostPerMember}; must be non-negative.");
                }

                if (needs.DailyHousingCost < 0)
                {
                    throw new SnapshotFormatException(
                        $"HouseholdNeeds {needs.HouseholdId} has invalid DailyHousingCost {needs.DailyHousingCost}; must be non-negative.");
                }

                if (needs.DailyUtilitiesCost < 0)
                {
                    throw new SnapshotFormatException(
                        $"HouseholdNeeds {needs.HouseholdId} has invalid DailyUtilitiesCost {needs.DailyUtilitiesCost}; must be non-negative.");
                }

                if (needs.ConsecutiveShortfallDays < 0)
                {
                    throw new SnapshotFormatException(
                        $"HouseholdNeeds {needs.HouseholdId} has invalid ConsecutiveShortfallDays {needs.ConsecutiveShortfallDays}; must be non-negative.");
                }

                if (needs.CumulativeShortfall < 0)
                {
                    throw new SnapshotFormatException(
                        $"HouseholdNeeds {needs.HouseholdId} has invalid CumulativeShortfall {needs.CumulativeShortfall}; must be non-negative.");
                }
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
            if (account.AccountId <= 0)
            {
                throw new SnapshotFormatException(
                    $"Account snapshot has invalid AccountId {account.AccountId}; must be positive (> 0).");
            }

            if (account.OwnerId <= 0)
            {
                throw new SnapshotFormatException(
                    $"Account snapshot has invalid OwnerId {account.OwnerId}; must be positive (> 0).");
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

    private static void ValidateCompaniesSnapshot(CompaniesSnapshotDto companies)
    {
        if (companies.Companies is null)
        {
            throw new SnapshotFormatException("Companies snapshot has null Companies collection.");
        }

        if (companies.JobPositions is null)
        {
            throw new SnapshotFormatException("Companies snapshot has null JobPositions collection.");
        }

        foreach (var company in companies.Companies)
        {
            if (company.CompanyId <= 0)
            {
                throw new SnapshotFormatException(
                    $"Company snapshot has invalid CompanyId {company.CompanyId}; must be positive (> 0).");
            }

            if (string.IsNullOrWhiteSpace(company.Name))
            {
                throw new SnapshotFormatException(
                    $"Company {company.CompanyId} has null or empty Name.");
            }

            if (company.Name.Length > Company.MaxNameLength)
            {
                throw new SnapshotFormatException(
                    $"Company {company.CompanyId} Name exceeds maximum length of {Company.MaxNameLength} characters.");
            }

            if (company.TreasuryAccountId <= 0)
            {
                throw new SnapshotFormatException(
                    $"Company {company.CompanyId} has invalid TreasuryAccountId {company.TreasuryAccountId}; must be positive (> 0).");
            }
        }

        foreach (var position in companies.JobPositions)
        {
            if (position.JobPositionId <= 0)
            {
                throw new SnapshotFormatException(
                    $"JobPosition snapshot has invalid JobPositionId {position.JobPositionId}; must be positive (> 0).");
            }

            if (position.CompanyId <= 0)
            {
                throw new SnapshotFormatException(
                    $"JobPosition {position.JobPositionId} has invalid CompanyId {position.CompanyId}; must be positive (> 0).");
            }

            if (string.IsNullOrWhiteSpace(position.JobRoleCode))
            {
                throw new SnapshotFormatException(
                    $"JobPosition {position.JobPositionId} has null or empty JobRoleCode.");
            }

            if (position.JobRoleCode.Length > JobRoleCode.MaxLength)
            {
                throw new SnapshotFormatException(
                    $"JobPosition {position.JobPositionId} JobRoleCode exceeds maximum length of {JobRoleCode.MaxLength} characters.");
            }

            if (position.PayAmountMinorUnits <= 0)
            {
                throw new SnapshotFormatException(
                    $"JobPosition {position.JobPositionId} has invalid PayAmountMinorUnits {position.PayAmountMinorUnits}; must be positive (> 0). Zero-pay positions are rejected.");
            }

            if (position.PayIntervalDays <= 0)
            {
                throw new SnapshotFormatException(
                    $"JobPosition {position.JobPositionId} has invalid PayIntervalDays {position.PayIntervalDays}; must be positive (> 0).");
            }

            if (string.IsNullOrWhiteSpace(position.Currency))
            {
                throw new SnapshotFormatException(
                    $"JobPosition {position.JobPositionId} has null or empty Currency.");
            }

            if (position.Currency.Length != 3)
            {
                throw new SnapshotFormatException(
                    $"JobPosition {position.JobPositionId} has invalid Currency '{position.Currency}'; must be exactly 3 characters.");
            }
        }
    }

    private static void ValidateEmploymentSnapshot(EmploymentSnapshotDto employment)
    {
        if (employment.Contracts is null)
        {
            throw new SnapshotFormatException("Employment snapshot has null Contracts collection.");
        }

        if (employment.PayrollStates is null)
        {
            throw new SnapshotFormatException("Employment snapshot has null PayrollStates collection.");
        }

        foreach (var contract in employment.Contracts)
        {
            if (contract.EmploymentContractId <= 0)
            {
                throw new SnapshotFormatException(
                    $"EmploymentContract snapshot has invalid EmploymentContractId {contract.EmploymentContractId}; must be positive (> 0).");
            }

            if (contract.PersonId <= 0)
            {
                throw new SnapshotFormatException(
                    $"EmploymentContract {contract.EmploymentContractId} has invalid PersonId {contract.PersonId}; must be positive (> 0).");
            }

            if (contract.CompanyId <= 0)
            {
                throw new SnapshotFormatException(
                    $"EmploymentContract {contract.EmploymentContractId} has invalid CompanyId {contract.CompanyId}; must be positive (> 0).");
            }

            if (contract.JobPositionId <= 0)
            {
                throw new SnapshotFormatException(
                    $"EmploymentContract {contract.EmploymentContractId} has invalid JobPositionId {contract.JobPositionId}; must be positive (> 0).");
            }

            if (contract.StartDayIndex < 0)
            {
                throw new SnapshotFormatException(
                    $"EmploymentContract {contract.EmploymentContractId} has invalid StartDayIndex {contract.StartDayIndex}; must be non-negative.");
            }

            if (contract.EndDayIndex < -1)
            {
                throw new SnapshotFormatException(
                    $"EmploymentContract {contract.EmploymentContractId} has invalid EndDayIndex {contract.EndDayIndex}; must be -1 (active) or >= 0 (terminated).");
            }

            if (contract.EndDayIndex >= 0 && contract.EndDayIndex < contract.StartDayIndex)
            {
                throw new SnapshotFormatException(
                    $"EmploymentContract {contract.EmploymentContractId} EndDayIndex ({contract.EndDayIndex}) cannot be before StartDayIndex ({contract.StartDayIndex}).");
            }

            if (contract.PayAmountMinorUnits <= 0)
            {
                throw new SnapshotFormatException(
                    $"EmploymentContract {contract.EmploymentContractId} has invalid PayAmountMinorUnits {contract.PayAmountMinorUnits}; must be positive (> 0). Zero-pay contracts are rejected.");
            }

            if (contract.PayIntervalDays <= 0)
            {
                throw new SnapshotFormatException(
                    $"EmploymentContract {contract.EmploymentContractId} has invalid PayIntervalDays {contract.PayIntervalDays}; must be positive (> 0).");
            }

            if (string.IsNullOrWhiteSpace(contract.Currency))
            {
                throw new SnapshotFormatException(
                    $"EmploymentContract {contract.EmploymentContractId} has null or empty Currency.");
            }

            if (contract.Currency.Length != 3)
            {
                throw new SnapshotFormatException(
                    $"EmploymentContract {contract.EmploymentContractId} has invalid Currency '{contract.Currency}'; must be exactly 3 characters.");
            }
        }

        // Validate 1:1 cardinality between Contracts and PayrollStates
        var contractIdSet = new HashSet<long>(employment.Contracts.Select(c => c.EmploymentContractId));
        var payrollContractIdSet = new HashSet<long>();

        foreach (var payroll in employment.PayrollStates)
        {
            if (payroll.EmploymentContractId <= 0)
            {
                throw new SnapshotFormatException(
                    $"PayrollState has invalid EmploymentContractId {payroll.EmploymentContractId}; must be positive (> 0).");
            }

            if (!payrollContractIdSet.Add(payroll.EmploymentContractId))
            {
                throw new SnapshotFormatException(
                    $"Duplicate PayrollState detected for contract {payroll.EmploymentContractId}.");
            }

            if (!contractIdSet.Contains(payroll.EmploymentContractId))
            {
                throw new SnapshotFormatException(
                    $"PayrollState references non-existent contract {payroll.EmploymentContractId} (orphan payroll).");
            }

            if (payroll.AccruedUnpaidWagesMinorUnits < 0)
            {
                throw new SnapshotFormatException(
                    $"PayrollState for contract {payroll.EmploymentContractId} has invalid AccruedUnpaidWagesMinorUnits {payroll.AccruedUnpaidWagesMinorUnits}; must be non-negative.");
            }

            if (payroll.ConsecutiveMissedPayPeriods < 0)
            {
                throw new SnapshotFormatException(
                    $"PayrollState for contract {payroll.EmploymentContractId} has invalid ConsecutiveMissedPayPeriods {payroll.ConsecutiveMissedPayPeriods}; must be non-negative.");
            }

            if (payroll.NextPayDayIndex < 0)
            {
                throw new SnapshotFormatException(
                    $"PayrollState for contract {payroll.EmploymentContractId} has invalid NextPayDayIndex {payroll.NextPayDayIndex}; must be non-negative.");
            }
        }

        foreach (var contractId in contractIdSet)
        {
            if (!payrollContractIdSet.Contains(contractId))
            {
                throw new SnapshotFormatException(
                    $"EmploymentContract {contractId} has no corresponding PayrollState (missing payroll).");
            }
        }
    }

    private static void ValidateCrossReferencesAndOwnership(SimulationSnapshotDto snapshot)
    {
        var population = snapshot.Population;
        var moneyLedger = snapshot.MoneyLedger;
        var companies = snapshot.Companies;
        var employment = snapshot.Employment;

        var personIds = population is not null
            ? new HashSet<long>(population.Individuals.Select(p => p.PersonId))
            : new HashSet<long>();

        var householdIds = population is not null
            ? new HashSet<long>(population.Households.Select(h => h.HouseholdId))
            : new HashSet<long>();

        var accountOwnerMap = moneyLedger is not null
            ? moneyLedger.Accounts.ToDictionary(a => a.AccountId, a => a.OwnerId)
            : new Dictionary<long, long>();

        var companyIds = companies is not null
            ? new HashSet<long>(companies.Companies.Select(c => c.CompanyId))
            : new HashSet<long>();

        var positionMap = companies is not null
            ? companies.JobPositions.ToDictionary(p => p.JobPositionId)
            : new Dictionary<long, JobPositionSnapshotDto>();

        // 1. Money supply accounting reconciliation
        if (moneyLedger is not null)
        {
            long sumBalances = 0;
            foreach (var account in moneyLedger.Accounts)
            {
                sumBalances = checked(sumBalances + account.BalanceMinorUnits);
            }

            long expectedSupply = checked(moneyLedger.TotalExternalCredits - moneyLedger.TotalExternalDebits);
            if (sumBalances != expectedSupply)
            {
                throw new SnapshotFormatException(
                    $"Money supply reconciliation failed: sum of account balances ({sumBalances}) does not match net external supply ({expectedSupply} = credits {moneyLedger.TotalExternalCredits} - debits {moneyLedger.TotalExternalDebits}).");
            }
        }

        // 2. Population cross-references and ownership
        if (population is not null)
        {
            foreach (var person in population.Individuals)
            {
                if (person.HouseholdId > 0 && !householdIds.Contains(person.HouseholdId))
                {
                    throw new SnapshotFormatException(
                        $"Person {person.PersonId} references non-existent household {person.HouseholdId}.");
                }

                if (person.PersonalAccountId > 0)
                {
                    if (!accountOwnerMap.TryGetValue(person.PersonalAccountId, out var owner))
                    {
                        throw new SnapshotFormatException(
                            $"Person {person.PersonId} references non-existent personal account {person.PersonalAccountId}.");
                    }

                    if (owner != person.PersonId)
                    {
                        throw new SnapshotFormatException(
                            $"Personal account {person.PersonalAccountId} for person {person.PersonId} is owned by {owner}, expected {person.PersonId}.");
                    }
                }
            }

            foreach (var household in population.Households)
            {
                if (!accountOwnerMap.TryGetValue(household.AccountId, out var owner))
                {
                    throw new SnapshotFormatException(
                        $"Household {household.HouseholdId} references non-existent account {household.AccountId}.");
                }

                if (owner != household.HouseholdId)
                {
                    throw new SnapshotFormatException(
                        $"Household account {household.AccountId} for household {household.HouseholdId} is owned by {owner}, expected {household.HouseholdId}.");
                }

                foreach (var memberId in household.MemberIds)
                {
                    if (!personIds.Contains(memberId))
                    {
                        throw new SnapshotFormatException(
                            $"Household {household.HouseholdId} references non-existent member person {memberId}.");
                    }
                }
            }

            foreach (var cohort in population.Cohorts)
            {
                if (cohort.HouseholdId > 0 && !householdIds.Contains(cohort.HouseholdId))
                {
                    throw new SnapshotFormatException(
                        $"Cohort {cohort.CohortId} references non-existent household {cohort.HouseholdId}.");
                }
            }

            if (population.HouseholdNeeds is not null)
            {
                foreach (var needs in population.HouseholdNeeds)
                {
                    if (!householdIds.Contains(needs.HouseholdId))
                    {
                        throw new SnapshotFormatException(
                            $"HouseholdNeeds references non-existent household {needs.HouseholdId}.");
                    }
                }
            }
        }

        // 3. Companies ownership, position references, and currency
        if (companies is not null)
        {
            foreach (var company in companies.Companies)
            {
                if (!accountOwnerMap.TryGetValue(company.TreasuryAccountId, out var owner))
                {
                    throw new SnapshotFormatException(
                        $"Company {company.CompanyId} references non-existent treasury account {company.TreasuryAccountId}.");
                }

                if (owner != company.CompanyId)
                {
                    throw new SnapshotFormatException(
                        $"Company treasury account {company.TreasuryAccountId} for company {company.CompanyId} is owned by {owner}, expected {company.CompanyId}.");
                }
            }

            foreach (var position in companies.JobPositions)
            {
                if (!companyIds.Contains(position.CompanyId))
                {
                    throw new SnapshotFormatException(
                        $"JobPosition {position.JobPositionId} references non-existent company {position.CompanyId}.");
                }

                if (moneyLedger is not null && position.Currency != moneyLedger.Currency)
                {
                    throw new SnapshotFormatException(
                        $"JobPosition {position.JobPositionId} currency '{position.Currency}' does not match ledger currency '{moneyLedger.Currency}'.");
                }
            }
        }

        // 4. Employment cross-references, occupancy, and currency
        if (employment is not null)
        {
            var activeOccupiedPositions = new HashSet<long>();

            foreach (var contract in employment.Contracts)
            {
                if (!personIds.Contains(contract.PersonId))
                {
                    throw new SnapshotFormatException(
                        $"EmploymentContract {contract.EmploymentContractId} references non-existent person {contract.PersonId}.");
                }

                if (!companyIds.Contains(contract.CompanyId))
                {
                    throw new SnapshotFormatException(
                        $"EmploymentContract {contract.EmploymentContractId} references non-existent company {contract.CompanyId}.");
                }

                if (!positionMap.TryGetValue(contract.JobPositionId, out var position))
                {
                    throw new SnapshotFormatException(
                        $"EmploymentContract {contract.EmploymentContractId} references non-existent job position {contract.JobPositionId}.");
                }

                if (position.CompanyId != contract.CompanyId)
                {
                    throw new SnapshotFormatException(
                        $"EmploymentContract {contract.EmploymentContractId} company {contract.CompanyId} does not match position's owning company {position.CompanyId}.");
                }

                if (moneyLedger is not null && contract.Currency != moneyLedger.Currency)
                {
                    throw new SnapshotFormatException(
                        $"EmploymentContract {contract.EmploymentContractId} currency '{contract.Currency}' does not match ledger currency '{moneyLedger.Currency}'.");
                }

                // Check active occupancy: at most one active contract per position
                if (contract.EndDayIndex == -1) // active
                {
                    if (!activeOccupiedPositions.Add(contract.JobPositionId))
                    {
                        throw new SnapshotFormatException(
                            $"Position {contract.JobPositionId} is occupied by multiple active employment contracts in snapshot.");
                    }
                }
            }
        }
    }

    private static void ApplyKernel(SimulationEngine engine, KernelStateDto kernel)
    {
        if (kernel.WorldSeed != engine.WorldSeed)
        {
            throw new SnapshotFormatException(
                $"Snapshot world seed ({kernel.WorldSeed}) does not match target engine's " +
                $"world seed ({engine.WorldSeed}). Construct engine with snapshot's seed before restoring.");
        }

        engine.Clock.Restore(new SimulationInstant(kernel.CurrentInstantSeconds));
        engine.EntityIds.Restore(kernel.NextEntityId);
        engine.Events.RestoreSequenceNumber(kernel.NextEventSequenceNumber);
    }

    private static void ApplyPopulation(SimulationEngine engine, PopulationSnapshotDto population)
    {
        var state = engine.State.Population;

        foreach (var household in population.Households)
        {
            state.CreateHousehold(
                new HouseholdId(new EntityId(household.HouseholdId)),
                new AccountId(new EntityId(household.AccountId)));
        }

        foreach (var person in population.Individuals)
        {
            var p = new Person(
                new PersonId(new EntityId(person.PersonId)),
                new HouseholdId(new EntityId(person.HouseholdId)),
                person.BirthDayIndex,
                person.PersonalAccountId > 0
                    ? new AccountId(new EntityId(person.PersonalAccountId))
                    : AccountId.None);
            state.AddPerson(p);
        }

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

        if (population.HouseholdNeeds is not null)
        {
            var currency = engine.State.MoneyLedger.Currency;
            foreach (var needs in population.HouseholdNeeds)
            {
                var householdId = new HouseholdId(new EntityId(needs.HouseholdId));
                var profile = new BasicNeedsProfile(
                    new Money(needs.DailyEssentialCostPerMember, currency),
                    new Money(needs.DailyHousingCost, currency),
                    new Money(needs.DailyUtilitiesCost, currency));

                var household = state.GetHousehold(householdId);
                if (household is not null)
                {
                    var needsState = new HouseholdNeedsState(householdId, profile, household.MemberCount);
                    needsState.Restore(
                        needs.ConsecutiveShortfallDays,
                        new Money(needs.CumulativeShortfall, currency));

                    state.SetHouseholdNeedsState(needsState);
                }
            }
        }
    }

    private static void ApplyMoneyLedger(SimulationEngine engine, MoneyLedgerSnapshotDto moneyLedger)
    {
        var ledger = engine.State.MoneyLedger;

        var accounts = moneyLedger.Accounts.Select(a =>
            (new AccountId(new EntityId(a.AccountId)),
             new EntityId(a.OwnerId),
             a.BalanceMinorUnits));

        ledger.Restore(accounts, moneyLedger.TotalExternalCredits, moneyLedger.TotalExternalDebits);
    }

    private static void ApplyCompanies(SimulationEngine engine, CompaniesSnapshotDto companies)
    {
        var state = engine.State.Companies;

        foreach (var companyDto in companies.Companies)
        {
            var company = new Company(
                new CompanyId(new EntityId(companyDto.CompanyId)),
                companyDto.Name,
                new AccountId(new EntityId(companyDto.TreasuryAccountId)));

            state.RestoreCompany(company);
        }

        foreach (var positionDto in companies.JobPositions)
        {
            var currency = new CurrencyCode(positionDto.Currency);
            var compensation = new CompensationTerms(
                positionDto.PayAmountMinorUnits,
                positionDto.PayIntervalDays,
                currency);
            var position = new JobPosition(
                new JobPositionId(new EntityId(positionDto.JobPositionId)),
                new CompanyId(new EntityId(positionDto.CompanyId)),
                JobRoleCode.FromString(positionDto.JobRoleCode),
                compensation);

            state.RestorePosition(position);
        }
    }

    private static void ApplyEmployment(SimulationEngine engine, EmploymentSnapshotDto employment)
    {
        var state = engine.State.Employment;

        foreach (var contractDto in employment.Contracts)
        {
            var currency = new CurrencyCode(contractDto.Currency);
            var compensation = new CompensationTerms(
                contractDto.PayAmountMinorUnits,
                contractDto.PayIntervalDays,
                currency);
            var contract = new EmploymentContract(
                new EmploymentContractId(new EntityId(contractDto.EmploymentContractId)),
                new PersonId(new EntityId(contractDto.PersonId)),
                new CompanyId(new EntityId(contractDto.CompanyId)),
                new JobPositionId(new EntityId(contractDto.JobPositionId)),
                contractDto.StartDayIndex,
                compensation);

            if (contractDto.EndDayIndex >= 0)
            {
                contract = contract.WithEndDayIndex(contractDto.EndDayIndex);
            }

            state.RestoreContract(contract);
        }

        foreach (var payrollDto in employment.PayrollStates)
        {
            var contractId = new EmploymentContractId(new EntityId(payrollDto.EmploymentContractId));
            var payroll = new PayrollState(contractId, payrollDto.NextPayDayIndex);
            payroll.Restore(
                payrollDto.AccruedUnpaidWagesMinorUnits,
                payrollDto.ConsecutiveMissedPayPeriods,
                payrollDto.NextPayDayIndex);

            state.RestorePayrollState(payroll);
        }
    }
}
