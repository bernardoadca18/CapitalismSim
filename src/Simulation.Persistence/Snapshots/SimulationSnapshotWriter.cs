using CapitalismSim.Simulation.Core.Companies;
using CapitalismSim.Simulation.Core.Economics;
using CapitalismSim.Simulation.Core.Engine;
using CapitalismSim.Simulation.Core.Entities;
using CapitalismSim.Simulation.Core.Employment;
using CapitalismSim.Simulation.Core.Population;

namespace CapitalismSim.Simulation.Persistence.Snapshots;

/// <summary>
/// Captures the complete simulation state into <see cref="SimulationSnapshotDto"/>.
/// </summary>
/// <remarks>
/// This is the authoritative writer for the top-level snapshot format.
/// All collections are captured with deterministic ordering (sorted by entity ID)
/// to ensure reproducible serialization.
/// </remarks>
public static class SimulationSnapshotWriter
{
    /// <summary>
    /// The snapshot schema version produced by this build.
    /// Increment when making breaking schema changes.
    /// </summary>
    public const int CurrentFormatVersion = 5;

    /// <summary>
    /// Captures the complete state of the simulation engine into a snapshot.
    /// </summary>
    public static SimulationSnapshotDto Capture(SimulationEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);

        return new SimulationSnapshotDto
        {
            FormatVersion = CurrentFormatVersion,
            Kernel = CaptureKernel(engine),
            Population = CapturePopulation(engine.State.Population),
            MoneyLedger = CaptureMoneyLedger(engine.State.MoneyLedger),
            Companies = CaptureCompanies(engine.State.Companies),
            Employment = CaptureEmployment(engine.State.Employment),
        };
    }

    private static KernelStateDto CaptureKernel(SimulationEngine engine)
    {
        return new KernelStateDto
        {
            WorldSeed = engine.WorldSeed,
            CurrentInstantSeconds = engine.Clock.Current.TotalSeconds,
            NextEntityId = engine.EntityIds.NextValue,
            NextEventSequenceNumber = engine.Events.NextSequenceNumber,
        };
    }

    private static PopulationSnapshotDto CapturePopulation(PopulationState population)
    {
        // Capture individuals sorted by PersonId for determinism
        var individuals = population.GetAllPersonIds()
            .OrderBy(id => id.EntityId.Value)
            .Select(personId =>
            {
                var person = population.GetPerson(personId);
                return new PersonSnapshotDto
                {
                    PersonId = person.Id.EntityId.Value,
                    HouseholdId = person.HouseholdId.EntityId.Value,
                    BirthDayIndex = person.BirthDayIndex,
                    PersonalAccountId = person.PersonalAccountId.EntityId.Value,
                };
            })
            .ToList();

        // Capture households sorted by HouseholdId for determinism
        var households = population.GetAllHouseholdIds()
            .OrderBy(id => id.EntityId.Value)
            .Select(householdId =>
            {
                var household = population.GetHousehold(householdId);
                return household is not null
                    ? new HouseholdSnapshotDto
                    {
                        HouseholdId = household.Id.EntityId.Value,
                        AccountId = household.AccountId.EntityId.Value,
                        MemberIds = household.Members
                            .Select(m => m.EntityId.Value)
                            .OrderBy(id => id)
                            .ToList(),
                    }
                    : null;
            })
            .Where(h => h is not null)
            .Cast<HouseholdSnapshotDto>()
            .ToList();

        // Capture cohorts sorted by CohortId for determinism
        var cohorts = population.GetAllCohortIds()
            .OrderBy(id => id.EntityId.Value)
            .Select(cohortId =>
            {
                var cohort = population.GetCohort(cohortId);
                return new CohortSnapshotDto
                {
                    CohortId = cohort.Id.EntityId.Value,
                    Count = cohort.Count,
                    HouseholdId = cohort.HouseholdId.EntityId.Value,
                };
            })
            .ToList();

        // Capture household needs sorted by HouseholdId for determinism
        var householdNeeds = population.GetAllHouseholdIds()
            .OrderBy(id => id.EntityId.Value)
            .Select(householdId =>
            {
                var needsState = population.GetHouseholdNeedsState(householdId);
                return needsState != null
                    ? new HouseholdNeedsSnapshotDto
                    {
                        HouseholdId = needsState.HouseholdId.EntityId.Value,
                        DailyEssentialCostPerMember = needsState.NeedsProfile.DailyEssentialCostPerMember.MinorUnits,
                        DailyHousingCost = needsState.NeedsProfile.DailyHousingCost.MinorUnits,
                        DailyUtilitiesCost = needsState.NeedsProfile.DailyUtilitiesCost.MinorUnits,
                        ConsecutiveShortfallDays = needsState.ConsecutiveShortfallDays,
                        CumulativeShortfall = needsState.CumulativeShortfall.MinorUnits,
                    }
                    : null;
            })
            .Where(n => n != null)
            .Cast<HouseholdNeedsSnapshotDto>()
            .ToList();

        return new PopulationSnapshotDto
        {
            Individuals = individuals,
            Households = households,
            Cohorts = cohorts,
            HouseholdNeeds = householdNeeds,
        };
    }

    private static MoneyLedgerSnapshotDto CaptureMoneyLedger(MoneyLedger ledger)
    {
        // Capture accounts sorted by AccountId for determinism
        var accounts = ledger.GetAllAccountIds()
            .OrderBy(id => id.EntityId.Value)
            .Select(accountId =>
            {
                var balance = ledger.GetBalance(accountId);
                var owner = ledger.GetOwner(accountId);
                return new AccountSnapshotDto
                {
                    AccountId = accountId.EntityId.Value,
                    OwnerId = owner.Value,
                    BalanceMinorUnits = balance.MinorUnits,
                };
            })
            .ToList();

        return new MoneyLedgerSnapshotDto
        {
            Currency = ledger.Currency.Code,
            Accounts = accounts,
            TotalExternalCredits = ledger.TotalExternalCredits.MinorUnits,
            TotalExternalDebits = ledger.TotalExternalDebits.MinorUnits,
        };
    }

    private static CompaniesSnapshotDto? CaptureCompanies(CompaniesState companies)
    {
        // Return null if no companies exist (pre-Phase 3 snapshots)
        if (companies.CompanyCount == 0 && companies.PositionCount == 0)
        {
            return null;
        }

        // Capture companies sorted by CompanyId for determinism
        var companyDtos = companies.GetAllCompanyIds()
            .OrderBy(id => id.EntityId.Value)
            .Select(companyId =>
            {
                var company = companies.GetCompany(companyId)!;
                return new CompanySnapshotDto
                {
                    CompanyId = company.Id.EntityId.Value,
                    Name = company.Name,
                    TreasuryAccountId = company.TreasuryAccountId.EntityId.Value,
                };
            })
            .ToList();

        // Capture positions sorted by JobPositionId for determinism
        var positionDtos = companies.GetAllPositionIds()
            .OrderBy(id => id.EntityId.Value)
            .Select(positionId =>
            {
                var position = companies.GetPosition(positionId);
                return new JobPositionSnapshotDto
                {
                    JobPositionId = position.Id.EntityId.Value,
                    CompanyId = position.CompanyId.EntityId.Value,
                    JobRoleCode = position.Role.Value,
                    PayAmountMinorUnits = position.PayAmountMinorUnits,
                    PayIntervalDays = position.PayIntervalDays,
                };
            })
            .ToList();

        return new CompaniesSnapshotDto
        {
            Companies = companyDtos,
            JobPositions = positionDtos,
        };
    }

    private static EmploymentSnapshotDto? CaptureEmployment(EmploymentState employment)
    {
        // Return null if no contracts exist (pre-Phase 3 snapshots)
        if (employment.ContractCount == 0)
        {
            return null;
        }

        // Capture contracts sorted by EmploymentContractId for determinism
        var contractDtos = employment.GetAllContractIds()
            .OrderBy(id => id.EntityId.Value)
            .Select(contractId =>
            {
                var contract = employment.GetContract(contractId);
                return new EmploymentContractSnapshotDto
                {
                    EmploymentContractId = contract.Id.EntityId.Value,
                    PersonId = contract.PersonId.EntityId.Value,
                    CompanyId = contract.CompanyId.EntityId.Value,
                    JobPositionId = contract.JobPositionId.EntityId.Value,
                    StartDayIndex = contract.StartDayIndex,
                    EndDayIndex = contract.EndDayIndex ?? -1, // -1 = active
                    PayAmountMinorUnits = contract.PayAmountMinorUnits,
                    PayIntervalDays = contract.PayIntervalDays,
                };
            })
            .ToList();

        // Capture payroll states sorted by EmploymentContractId for determinism
        var payrollDtos = employment.GetAllContractIds()
            .OrderBy(id => id.EntityId.Value)
            .Select(contractId =>
            {
                var payroll = employment.GetPayrollState(contractId);
                return new PayrollStateSnapshotDto
                {
                    EmploymentContractId = contractId.EntityId.Value,
                    AccruedUnpaidWagesMinorUnits = payroll?.AccruedUnpaidWagesMinorUnits ?? 0,
                    ConsecutiveMissedPayPeriods = payroll?.ConsecutiveMissedPayPeriods ?? 0,
                    NextPayDayIndex = payroll?.NextPayDayIndex ?? 0,
                };
            })
            .ToList();

        return new EmploymentSnapshotDto
        {
            Contracts = contractDtos,
            PayrollStates = payrollDtos,
        };
    }
}
