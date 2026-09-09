using CapitalismSim.Simulation.Core.Engine;
using CapitalismSim.Simulation.Core.Entities;
using CapitalismSim.Simulation.Core.State;

namespace CapitalismSim.Simulation.Core.Diagnostics;

/// <summary>
/// Computes the authoritative canonical semantic fingerprint of a complete simulation state.
/// This includes kernel state, population, money ledger, companies, and employment,
/// sorting all collections deterministically by numeric EntityId.
/// Non-semantic data (wall clock time, memory allocation, execution counters) is explicitly excluded.
/// </summary>
public static class SimulationStateFingerprint
{
    public static string Compute(SimulationEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        return Compute(
            engine.WorldSeed,
            engine.Clock.Current.TotalSeconds,
            engine.EntityIds.NextValue,
            engine.Events.NextSequenceNumber,
            engine.State);
    }

    public static string Compute(
        ulong worldSeed,
        long currentInstantSeconds,
        long nextEntityId,
        long nextEventSequenceNumber,
        SimulationState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var builder = new DeterministicFingerprintBuilder();

        // 1. Kernel state
        builder.Add(worldSeed);
        builder.Add(currentInstantSeconds);
        builder.Add(nextEntityId);
        builder.Add(nextEventSequenceNumber);

        // 2. Population state
        var population = state.Population;

        // Persons sorted by PersonId
        var personIds = population.GetAllPersonIds()
            .OrderBy(id => id.EntityId.Value)
            .ToList();
        builder.Add(personIds.Count);
        foreach (var personId in personIds)
        {
            var person = population.GetPerson(personId);
            builder.Add(person.Id.EntityId.Value);
            builder.Add(person.HouseholdId.EntityId.Value);
            builder.Add(person.BirthDayIndex);
            builder.Add(person.PersonalAccountId.EntityId.Value);
        }

        // Households sorted by HouseholdId
        var householdIds = population.GetAllHouseholdIds()
            .OrderBy(id => id.EntityId.Value)
            .ToList();
        builder.Add(householdIds.Count);
        foreach (var householdId in householdIds)
        {
            var household = population.GetHousehold(householdId);
            if (household is null) continue;
            builder.Add(household.Id.EntityId.Value);
            builder.Add(household.AccountId.EntityId.Value);
            var memberIds = household.Members
                .Select(m => m.EntityId.Value)
                .OrderBy(v => v)
                .ToList();
            builder.Add(memberIds.Count);
            foreach (var mId in memberIds)
            {
                builder.Add(mId);
            }
        }

        // Cohorts sorted by CohortId
        var cohortIds = population.GetAllCohortIds()
            .OrderBy(id => id.EntityId.Value)
            .ToList();
        builder.Add(cohortIds.Count);
        foreach (var cohortId in cohortIds)
        {
            var cohort = population.GetCohort(cohortId);
            builder.Add(cohort.Id.EntityId.Value);
            builder.Add(cohort.Count);
            builder.Add(cohort.HouseholdId.EntityId.Value);
        }

        // Household Needs sorted by HouseholdId
        foreach (var householdId in householdIds)
        {
            var needs = population.GetHouseholdNeedsState(householdId);
            if (needs is null) continue;
            builder.Add(needs.HouseholdId.EntityId.Value);
            builder.Add(needs.NeedsProfile.DailyEssentialCostPerMember.MinorUnits);
            builder.Add(needs.NeedsProfile.DailyHousingCost.MinorUnits);
            builder.Add(needs.NeedsProfile.DailyUtilitiesCost.MinorUnits);
            builder.Add(needs.ConsecutiveShortfallDays);
            builder.Add(needs.CumulativeShortfall.MinorUnits);
        }

        // 3. Money Ledger
        var ledger = state.MoneyLedger;
        builder.Add(ledger.Currency.Code);
        var accountIds = ledger.GetAllAccountIds()
            .OrderBy(id => id.EntityId.Value)
            .ToList();
        builder.Add(accountIds.Count);
        foreach (var accountId in accountIds)
        {
            builder.Add(accountId.EntityId.Value);
            builder.Add(ledger.GetOwner(accountId).Value);
            builder.Add(ledger.GetBalance(accountId).MinorUnits);
        }
        builder.Add(ledger.TotalExternalCredits.MinorUnits);
        builder.Add(ledger.TotalExternalDebits.MinorUnits);

        // 4. Companies
        var companies = state.Companies;
        var companyIds = companies.GetAllCompanyIds()
            .OrderBy(id => id.EntityId.Value)
            .ToList();
        builder.Add(companyIds.Count);
        foreach (var companyId in companyIds)
        {
            var company = companies.GetCompany(companyId);
            if (company is null) continue;
            builder.Add(company.Id.EntityId.Value);
            builder.Add(company.Name);
            builder.Add(company.TreasuryAccountId.EntityId.Value);
        }

        var positionIds = companies.GetAllPositionIds()
            .OrderBy(id => id.EntityId.Value)
            .ToList();
        builder.Add(positionIds.Count);
        foreach (var positionId in positionIds)
        {
            var position = companies.GetPosition(positionId);
            builder.Add(position.Id.EntityId.Value);
            builder.Add(position.CompanyId.EntityId.Value);
            builder.Add(position.Role.Value);
            builder.Add(position.PayAmountMinorUnits);
            builder.Add(position.Compensation.Currency.Code);
            builder.Add(position.PayIntervalDays);
        }

        // 5. Employment
        var employment = state.Employment;
        var contractIds = employment.GetAllContractIds()
            .OrderBy(id => id.EntityId.Value)
            .ToList();
        builder.Add(contractIds.Count);
        foreach (var contractId in contractIds)
        {
            var contract = employment.GetContract(contractId);
            builder.Add(contract.Id.EntityId.Value);
            builder.Add(contract.PersonId.EntityId.Value);
            builder.Add(contract.CompanyId.EntityId.Value);
            builder.Add(contract.JobPositionId.EntityId.Value);
            builder.Add(contract.StartDayIndex);
            builder.Add(contract.EndDayIndex ?? -1L);
            builder.Add(contract.PayAmountMinorUnits);
            builder.Add(contract.Compensation.Currency.Code);
            builder.Add(contract.PayIntervalDays);

            var payroll = employment.GetPayrollState(contractId);
            if (payroll is not null)
            {
                builder.Add(payroll.ContractId.EntityId.Value);
                builder.Add(payroll.AccruedUnpaidWagesMinorUnits);
                builder.Add(payroll.ConsecutiveMissedPayPeriods);
                builder.Add(payroll.NextPayDayIndex);
            }
        }

        return builder.Build();
    }
}
