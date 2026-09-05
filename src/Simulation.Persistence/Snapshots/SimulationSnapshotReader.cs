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
/// The caller is responsible for constructing a new <see cref="SimulationEngine"/>
/// with the same <see cref="SimulationEngineOptions.WorldSeed"/> as the
/// snapshot before calling <see cref="Apply"/>.
/// </remarks>
public static class SimulationSnapshotReader
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
    public static void Apply(SimulationEngine engine, SimulationSnapshotDto snapshot)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(snapshot);

        ValidateFormatVersion(snapshot.FormatVersion);
        ValidateKernelSnapshot(snapshot.Kernel);

        // Phase 2 snapshot validation (optional for backwards compatibility)
        if (snapshot.Population is not null)
        {
            ValidatePopulationSnapshot(snapshot.Population);
        }

        if (snapshot.MoneyLedger is not null)
        {
            ValidateMoneyLedgerSnapshot(snapshot.MoneyLedger);
        }

        // Cross-reference validation between Population and MoneyLedger
        if (snapshot.Population is not null && snapshot.MoneyLedger is not null)
        {
            ValidateCrossReferences(snapshot.Population, snapshot.MoneyLedger);
        }

        // Phase 3 validation (when present)
        if (snapshot.Companies is not null)
        {
            ValidateCompaniesSnapshot(snapshot.Companies);
        }

        if (snapshot.Employment is not null)
        {
            ValidateEmploymentSnapshot(snapshot.Employment);
        }

        // Cross-reference validation for Phase 3
        if (snapshot.Companies is not null && snapshot.Employment is not null)
        {
            ValidateCompaniesEmploymentCrossReferences(snapshot.Companies, snapshot.Employment);
        }

        if (snapshot.Population is not null && snapshot.Employment is not null)
        {
            ValidatePopulationEmploymentCrossReferences(snapshot.Population, snapshot.Employment);
        }

        // Apply kernel state
        ApplyKernel(engine, snapshot.Kernel);

        // Apply Phase 2 state if present
        if (snapshot.Population is not null)
        {
            ApplyPopulation(engine, snapshot.Population);
        }

        if (snapshot.MoneyLedger is not null)
        {
            ApplyMoneyLedger(engine, snapshot.MoneyLedger);
        }

        // Apply Phase 3 state if present
        if (snapshot.Companies is not null)
        {
            ApplyCompanies(engine, snapshot.Companies);
        }

        if (snapshot.Employment is not null)
        {
            ApplyEmployment(engine, snapshot.Employment, snapshot.Companies);
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
                person.BirthDayIndex,
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

        // Restore household needs states
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
                    // Restore shortfall tracking directly from snapshot
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

        // Prepare account data for direct restoration
        var accounts = moneyLedger.Accounts.Select(a =>
            (new AccountId(new EntityId(a.AccountId)),
             new EntityId(a.OwnerId),
             a.BalanceMinorUnits));

        ledger.Restore(accounts, moneyLedger.TotalExternalCredits, moneyLedger.TotalExternalDebits);
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

        if (population.HouseholdNeeds is not null)
        {
            foreach (var needs in population.HouseholdNeeds)
            {
                if (needs.HouseholdId < 0)
                {
                    throw new SnapshotFormatException(
                        $"HouseholdNeeds snapshot has invalid HouseholdId {needs.HouseholdId}; must be non-negative.");
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

    private static void ValidateCrossReferences(PopulationSnapshotDto population, MoneyLedgerSnapshotDto moneyLedger)
    {
        var householdIds = new HashSet<long>(population.Households.Select(h => h.HouseholdId));
        var accountIds = new HashSet<long>(moneyLedger.Accounts.Select(a => a.AccountId));

        // Validate persons reference existing households
        foreach (var person in population.Individuals)
        {
            if (person.HouseholdId > 0 && !householdIds.Contains(person.HouseholdId))
            {
                throw new SnapshotFormatException(
                    $"Person {person.PersonId} references non-existent household {person.HouseholdId}.");
            }
        }

        // Validate households reference existing accounts
        foreach (var household in population.Households)
        {
            if (!accountIds.Contains(household.AccountId))
            {
                throw new SnapshotFormatException(
                    $"Household {household.HouseholdId} references non-existent account {household.AccountId}.");
            }
        }

        // Validate cohorts reference existing households (0 means unattached)
        foreach (var cohort in population.Cohorts)
        {
            if (cohort.HouseholdId > 0 && !householdIds.Contains(cohort.HouseholdId))
            {
                throw new SnapshotFormatException(
                    $"Cohort {cohort.CohortId} references non-existent household {cohort.HouseholdId}.");
            }
        }

        // Validate household needs reference existing households
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
            if (company.CompanyId < 0)
            {
                throw new SnapshotFormatException(
                    $"Company snapshot has invalid CompanyId {company.CompanyId}; must be non-negative.");
            }

            if (string.IsNullOrWhiteSpace(company.Name))
            {
                throw new SnapshotFormatException(
                    $"Company {company.CompanyId} has null or empty Name.");
            }

            if (company.TreasuryAccountId < 0)
            {
                throw new SnapshotFormatException(
                    $"Company {company.CompanyId} has invalid TreasuryAccountId {company.TreasuryAccountId}; must be non-negative.");
            }
        }

        var companyIds = new HashSet<long>(companies.Companies.Select(c => c.CompanyId));

        foreach (var position in companies.JobPositions)
        {
            if (position.JobPositionId < 0)
            {
                throw new SnapshotFormatException(
                    $"JobPosition snapshot has invalid JobPositionId {position.JobPositionId}; must be non-negative.");
            }

            if (!companyIds.Contains(position.CompanyId))
            {
                throw new SnapshotFormatException(
                    $"JobPosition {position.JobPositionId} references non-existent company {position.CompanyId}.");
            }

            if (string.IsNullOrWhiteSpace(position.JobRoleCode))
            {
                throw new SnapshotFormatException(
                    $"JobPosition {position.JobPositionId} has null or empty JobRoleCode.");
            }

            if (position.PayAmountMinorUnits < 0)
            {
                throw new SnapshotFormatException(
                    $"JobPosition {position.JobPositionId} has invalid PayAmountMinorUnits {position.PayAmountMinorUnits}; must be non-negative.");
            }

            if (position.PayIntervalDays <= 0)
            {
                throw new SnapshotFormatException(
                    $"JobPosition {position.JobPositionId} has invalid PayIntervalDays {position.PayIntervalDays}; must be positive.");
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
            if (contract.EmploymentContractId < 0)
            {
                throw new SnapshotFormatException(
                    $"EmploymentContract snapshot has invalid EmploymentContractId {contract.EmploymentContractId}; must be non-negative.");
            }

            if (contract.PersonId < 0)
            {
                throw new SnapshotFormatException(
                    $"EmploymentContract {contract.EmploymentContractId} has invalid PersonId {contract.PersonId}; must be non-negative.");
            }

            if (contract.CompanyId < 0)
            {
                throw new SnapshotFormatException(
                    $"EmploymentContract {contract.EmploymentContractId} has invalid CompanyId {contract.CompanyId}; must be non-negative.");
            }

            if (contract.JobPositionId < 0)
            {
                throw new SnapshotFormatException(
                    $"EmploymentContract {contract.EmploymentContractId} has invalid JobPositionId {contract.JobPositionId}; must be non-negative.");
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

            if (contract.PayAmountMinorUnits < 0)
            {
                throw new SnapshotFormatException(
                    $"EmploymentContract {contract.EmploymentContractId} has invalid PayAmountMinorUnits {contract.PayAmountMinorUnits}; must be non-negative.");
            }

            if (contract.PayIntervalDays <= 0)
            {
                throw new SnapshotFormatException(
                    $"EmploymentContract {contract.EmploymentContractId} has invalid PayIntervalDays {contract.PayIntervalDays}; must be positive.");
            }
        }

        var contractIds = new HashSet<long>(employment.Contracts.Select(c => c.EmploymentContractId));

        foreach (var payroll in employment.PayrollStates)
        {
            if (!contractIds.Contains(payroll.EmploymentContractId))
            {
                throw new SnapshotFormatException(
                    $"PayrollState references non-existent contract {payroll.EmploymentContractId}.");
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
    }

    private static void ValidateCompaniesEmploymentCrossReferences(CompaniesSnapshotDto companies, EmploymentSnapshotDto employment)
    {
        var companyIds = new HashSet<long>(companies.Companies.Select(c => c.CompanyId));
        var jobPositionIds = new HashSet<long>(companies.JobPositions.Select(p => p.JobPositionId));

        foreach (var contract in employment.Contracts)
        {
            if (!companyIds.Contains(contract.CompanyId))
            {
                throw new SnapshotFormatException(
                    $"EmploymentContract {contract.EmploymentContractId} references non-existent company {contract.CompanyId}.");
            }

            if (!jobPositionIds.Contains(contract.JobPositionId))
            {
                throw new SnapshotFormatException(
                    $"EmploymentContract {contract.EmploymentContractId} references non-existent job position {contract.JobPositionId}.");
            }
        }
    }

    private static void ValidatePopulationEmploymentCrossReferences(PopulationSnapshotDto population, EmploymentSnapshotDto employment)
    {
        var personIds = new HashSet<long>(population.Individuals.Select(p => p.PersonId));

        foreach (var contract in employment.Contracts)
        {
            if (!personIds.Contains(contract.PersonId))
            {
                throw new SnapshotFormatException(
                    $"EmploymentContract {contract.EmploymentContractId} references non-existent person {contract.PersonId}.");
            }
        }
    }

    private static void ApplyCompanies(SimulationEngine engine, CompaniesSnapshotDto companies)
    {
        var state = engine.State.Companies;

        // Restore companies first (positions reference companies)
        foreach (var companyDto in companies.Companies)
        {
            var company = new Company(
                new CompanyId(new EntityId(companyDto.CompanyId)),
                companyDto.Name,
                new AccountId(new EntityId(companyDto.TreasuryAccountId)));

            // Use internal restoration to bypass validation (treasury account validation requires ledger state)
            state.RestoreCompany(company);
        }

        // Restore job positions
        foreach (var positionDto in companies.JobPositions)
        {
            var position = new JobPosition(
                new JobPositionId(new EntityId(positionDto.JobPositionId)),
                new CompanyId(new EntityId(positionDto.CompanyId)),
                JobRoleCode.FromString(positionDto.JobRoleCode),
                positionDto.PayAmountMinorUnits,
                positionDto.PayIntervalDays);

            state.RestorePosition(position);
        }
    }

    private static void ApplyEmployment(SimulationEngine engine, EmploymentSnapshotDto employment, CompaniesSnapshotDto? companies)
    {
        var state = engine.State.Employment;

        // Restore employment contracts
        foreach (var contractDto in employment.Contracts)
        {
            var contract = new EmploymentContract(
                new EmploymentContractId(new EntityId(contractDto.EmploymentContractId)),
                new PersonId(new EntityId(contractDto.PersonId)),
                new CompanyId(new EntityId(contractDto.CompanyId)),
                new JobPositionId(new EntityId(contractDto.JobPositionId)),
                contractDto.StartDayIndex,
                contractDto.PayAmountMinorUnits,
                contractDto.PayIntervalDays);

            // -1 = active (null EndDayIndex), >= 0 = terminated on that day
            if (contractDto.EndDayIndex >= 0)
            {
                contract = contract.WithEndDayIndex(contractDto.EndDayIndex);
            }

            state.RestoreContract(contract);
        }

        // Restore payroll states
        foreach (var payrollDto in employment.PayrollStates)
        {
            var contractId = new EmploymentContractId(new EntityId(payrollDto.EmploymentContractId));
            var payroll = state.GetPayrollState(contractId);

            if (payroll is not null)
            {
                payroll.Restore(
                    payrollDto.AccruedUnpaidWagesMinorUnits,
                    payrollDto.ConsecutiveMissedPayPeriods,
                    payrollDto.NextPayDayIndex);
            }
        }
    }
}
