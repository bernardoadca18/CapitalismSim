using CapitalismSim.Simulation.Core.Economics;
using CapitalismSim.Simulation.Core.Engine;
using CapitalismSim.Simulation.Core.Entities;
using CapitalismSim.Simulation.Core.Population;

namespace CapitalismSim.Simulation.Persistence.Snapshots;

/// <summary>
/// Captures the current state of a <see cref="SimulationEngine"/> into a
/// <see cref="KernelSnapshotDto"/>.
/// </summary>
public static class KernelSnapshotWriter
{
    /// <summary>
    /// The snapshot schema version produced by this build. Bump this and
    /// add explicit migration/rejection logic whenever the schema changes
    /// incompatibly.
    /// </summary>
    public const int CurrentFormatVersion = 4;

    public static KernelSnapshotDto Capture(SimulationEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);

        return new KernelSnapshotDto
        {
            FormatVersion = CurrentFormatVersion,
            WorldSeed = engine.WorldSeed,
            CurrentInstantSeconds = engine.Clock.Current.TotalSeconds,
            NextEntityId = engine.EntityIds.NextValue,
            NextEventSequenceNumber = engine.Events.NextSequenceNumber,
            Population = CapturePopulation(engine.State.Population),
            MoneyLedger = CaptureMoneyLedger(engine.State.MoneyLedger),
        };
    }

    private static PopulationSnapshotDto CapturePopulation(PopulationState population)
    {
        var individuals = new List<PersonSnapshotDto>();
        foreach (var personId in population.GetAllPersonIds())
        {
            var person = population.GetPerson(personId);
            individuals.Add(new PersonSnapshotDto
            {
                PersonId = person.Id.EntityId.Value,
                HouseholdId = person.HouseholdId.EntityId.Value,
                Age = person.Age,
                PersonalAccountId = person.PersonalAccountId.EntityId.Value,
            });
        }

        var households = new List<HouseholdSnapshotDto>();
        foreach (var householdId in population.GetAllHouseholdIds())
        {
            var household = population.GetHousehold(householdId);
            if (household is not null)
            {
                households.Add(new HouseholdSnapshotDto
                {
                    HouseholdId = household.Id.EntityId.Value,
                    AccountId = household.AccountId.EntityId.Value,
                    MemberIds = household.Members.Select(m => m.EntityId.Value).ToList(),
                });
            }
        }

        var cohorts = new List<CohortSnapshotDto>();
        foreach (var cohortId in population.GetAllCohortIds())
        {
            var cohort = population.GetCohort(cohortId);
            cohorts.Add(new CohortSnapshotDto
            {
                CohortId = cohort.Id.EntityId.Value,
                Count = cohort.Count,
                HouseholdId = cohort.HouseholdId.EntityId.Value,
            });
        }

        return new PopulationSnapshotDto
        {
            Individuals = individuals,
            Households = households,
            Cohorts = cohorts,
        };
    }

    private static MoneyLedgerSnapshotDto CaptureMoneyLedger(MoneyLedger ledger)
    {
        var accounts = new List<AccountSnapshotDto>();
        foreach (var accountId in ledger.GetAllAccountIds())
        {
            var balance = ledger.GetBalance(accountId);
            var owner = ledger.GetOwner(accountId);
            accounts.Add(new AccountSnapshotDto
            {
                AccountId = accountId.EntityId.Value,
                OwnerId = owner.Value,
                BalanceMinorUnits = balance.MinorUnits,
            });
        }

        return new MoneyLedgerSnapshotDto
        {
            Currency = ledger.Currency.Code,
            Accounts = accounts,
            TotalExternalCredits = ledger.TotalExternalCredits.MinorUnits,
            TotalExternalDebits = ledger.TotalExternalDebits.MinorUnits,
        };
    }
}
