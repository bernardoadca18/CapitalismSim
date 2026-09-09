using CapitalismSim.Simulation.Core.Companies;
using CapitalismSim.Simulation.Core.Diagnostics;
using CapitalismSim.Simulation.Core.Economics;
using CapitalismSim.Simulation.Core.Entities;
using CapitalismSim.Simulation.Core.Engine;
using CapitalismSim.Simulation.Core.Employment;
using CapitalismSim.Simulation.Core.Population;
using CapitalismSim.Simulation.Core.Scheduling;
using CapitalismSim.Simulation.Core.Systems;
using CapitalismSim.Simulation.Core.Time;
using CapitalismSim.Simulation.Persistence.Snapshots;

namespace CapitalismSim.Simulation.Headless.Scenarios;

/// <summary>
/// Phase 3 demonstration scenario: companies, employment, and payroll.
/// Builds on Phase 2 (population with basic needs) and adds:
/// - Companies with treasury accounts
/// - Job positions with compensation
/// - Employment contracts linking persons to positions
/// - PayrollSystem for wage payments
/// </summary>
/// <remarks>
/// This scenario demonstrates:
/// - CompaniesState with companies and job positions
/// - EmploymentState with contracts and payroll tracking
/// - PayrollSystem transferring wages from company treasuries to employee accounts
/// - Wage arrears when companies cannot pay
/// </remarks>
public sealed class Phase3Scenario
{
    public required SimulationEngine Engine { get; init; }
    public required HouseholdNeedsSystem NeedsSystem { get; init; }
    public required PayrollSystem PayrollSystem { get; init; }
    public required CurrencyCode Currency { get; init; }

    public static Phase3Scenario Create(
        ulong seed,
        int companyCount = 2,
        int positionsPerCompany = 5,
        int householdCount = 10,
        int personsPerHousehold = 4)
    {
        var currency = CurrencyCode.USD;
        var engine = new SimulationEngine(new SimulationEngineOptions
        {
            WorldSeed = seed,
            StartInstant = SimulationInstant.Epoch,
            Currency = currency,
        });

        // Register systems
        var needsSystem = new HouseholdNeedsSystem(currency);
        engine.Scheduler.Register(needsSystem, new SchedulingPolicy(
            SimulationInstant.Epoch,
            SimulationDuration.FromDays(1))); // Daily

        var payrollSystem = new PayrollSystem();
        engine.Scheduler.Register(payrollSystem, new SchedulingPolicy(
            SimulationInstant.Epoch,
            SimulationDuration.FromDays(1))); // Daily

        // Populate the simulation
        PopulateSimulation(engine, companyCount, positionsPerCompany, householdCount, personsPerHousehold, currency);

        return new Phase3Scenario
        {
            Engine = engine,
            NeedsSystem = needsSystem,
            PayrollSystem = payrollSystem,
            Currency = currency,
        };
    }

    private static void PopulateSimulation(
        SimulationEngine engine,
        int companyCount,
        int positionsPerCompany,
        int householdCount,
        int personsPerHousehold,
        CurrencyCode currency)
    {
        var population = engine.State.Population;
        var ledger = engine.State.MoneyLedger;
        var companies = engine.State.Companies;
        var employment = engine.State.Employment;

        // Create households and persons first
        var allPersons = new List<PersonId>();
        for (int h = 0; h < householdCount; h++)
        {
            // Create household with account
            var householdEntityId = engine.EntityIds.Allocate();
            var householdId = new HouseholdId(householdEntityId);
            var accountEntityId = engine.EntityIds.Allocate();
            var accountId = new AccountId(accountEntityId);

            population.CreateHousehold(householdId, accountId);
            ledger.CreateAccount(accountId, householdEntityId);

            // Initial endowment: $3000 per household (less than Phase 2 to simulate need for wages)
            ledger.ExternalCredit(accountId, new Money(300000, currency), MoneyFlowReason.InitialEndowment);

            // Create persons in household
            for (int p = 0; p < personsPerHousehold; p++)
            {
                var personEntityId = engine.EntityIds.Allocate();
                var personId = new PersonId(personEntityId);

                // Create personal account for the person
                var personalAccountEntityId = engine.EntityIds.Allocate();
                var personalAccountId = new AccountId(personalAccountEntityId);
                ledger.CreateAccount(personalAccountId, personEntityId);

                // Create person (age 18-65 for working adults at epoch)
                int age = 18 + (int)(h * personsPerHousehold + p) % 48;
                var person = Person.WithAge(personId, householdId, age, referenceSimulationDay: 0, personalAccountId);
                population.AddPerson(person);

                allPersons.Add(personId);
            }
        }

        // Create companies with treasury accounts
        var allPositions = new List<(CompanyId CompanyId, JobPositionId PositionId, CompensationTerms Compensation)>();
        for (int c = 0; c < companyCount; c++)
        {
            var companyEntityId = engine.EntityIds.Allocate();
            var companyId = new CompanyId(companyEntityId);
            var treasuryAccountEntityId = engine.EntityIds.Allocate();
            var treasuryAccountId = new AccountId(treasuryAccountEntityId);

            // Create treasury account first (required by CreateCompany)
            ledger.CreateAccount(treasuryAccountId, companyEntityId);
            // Initial treasury: $100,000 per company
            ledger.ExternalCredit(treasuryAccountId, new Money(10000000, currency), MoneyFlowReason.InitialEndowment);

            // Create company
            var company = new Company(companyId, $"Company-{c}", treasuryAccountId);
            companies.CreateCompany(company, ledger);

            // Create job positions
            for (int p = 0; p < positionsPerCompany; p++)
            {
                var positionEntityId = engine.EntityIds.Allocate();
                var positionId = new JobPositionId(positionEntityId);

                // Compensation: $5000/pay period (monthly), varying by position
                long payAmount = 400000 + (p * 50000); // $4000-$6500 per month
                var compensation = new CompensationTerms(payAmount, 30, currency); // 30-day pay interval

                var position = new JobPosition(positionId, companyId, JobRoleCode.GeneralWorker, compensation);
                companies.AddPosition(position);

                allPositions.Add((companyId, positionId, compensation));
            }
        }

        // Register HirePersonCommandHandler on the simulation command dispatcher
        var hireHandler = new HirePersonCommandHandler(
            population,
            companies,
            employment,
            ledger,
            engine.EntityIds);
        engine.Commands.RegisterHandler(hireHandler);

        // Hire persons for available positions using the public HirePersonCommand
        int personIndex = 0;
        foreach (var (companyId, positionId, compensation) in allPositions)
        {
            if (personIndex >= allPersons.Count)
                break;

            var personId = allPersons[personIndex++];

            engine.Commands.Dispatch(new HirePersonCommand
            {
                PersonId = personId,
                JobPositionId = positionId,
                StartDayIndex = 0,
            });
        }
    }

    /// <summary>
    /// Advances the simulation by the given number of simulated days.
    /// </summary>
    public void AdvanceByDays(int days)
    {
        Engine.AdvanceBy(SimulationDuration.FromDays(days));
    }

    /// <summary>
    /// Restores a Phase3Scenario from a snapshot, including system re-registration.
    /// </summary>
    public static Phase3Scenario FromSnapshot(SimulationSnapshotDto snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var currency = snapshot.MoneyLedger is not null
            ? new CurrencyCode(snapshot.MoneyLedger.Currency)
            : CurrencyCode.USD;

        var engine = new SimulationEngine(new SimulationEngineOptions
        {
            WorldSeed = snapshot.Kernel.WorldSeed,
            StartInstant = SimulationInstant.Epoch,
            Currency = currency,
        });

        // Register systems (required before applying snapshot so they're scheduled correctly)
        var needsSystem = new HouseholdNeedsSystem(currency);
        engine.Scheduler.Register(needsSystem, new SchedulingPolicy(
            SimulationInstant.Epoch,
            SimulationDuration.FromDays(1)));

        var payrollSystem = new PayrollSystem();
        engine.Scheduler.Register(payrollSystem, new SchedulingPolicy(
            SimulationInstant.Epoch,
            SimulationDuration.FromDays(1)));

        // Apply snapshot state
        SimulationSnapshotReader.Apply(engine, snapshot);

        // Register command handlers on the restored engine
        var hireHandler = new HirePersonCommandHandler(
            engine.State.Population,
            engine.State.Companies,
            engine.State.Employment,
            engine.State.MoneyLedger,
            engine.EntityIds);
        engine.Commands.RegisterHandler(hireHandler);

        return new Phase3Scenario
        {
            Engine = engine,
            NeedsSystem = needsSystem,
            PayrollSystem = payrollSystem,
            Currency = currency,
        };
    }

    /// <summary>
    /// Validates full simulation integrity for Phase 3.
    /// </summary>
    public (bool IsValid, IReadOnlyList<string> Violations) ValidateIntegrity()
    {
        var violations = new List<string>();

        var popResult = Engine.State.Population.ValidateIntegrity(Engine.State.MoneyLedger);
        if (!popResult.IsValid)
        {
            violations.AddRange(popResult.Violations);
        }

        var compResult = Engine.State.Companies.ValidateIntegrity(Engine.State.MoneyLedger);
        if (!compResult.IsValid)
        {
            violations.AddRange(compResult.Violations);
        }

        var empResult = Engine.State.Employment.ValidateIntegrity(Engine.State.Population, Engine.State.Companies);
        if (!empResult.IsValid)
        {
            violations.AddRange(empResult.Violations);
        }

        if (!Engine.State.MoneyLedger.VerifySupplyAccounting())
        {
            violations.Add("Money ledger supply accounting invariant violated.");
        }

        var ledger = Engine.State.MoneyLedger;
        var population = Engine.State.Population;
        var companies = Engine.State.Companies;
        var employment = Engine.State.Employment;

        // Account ownership checks
        foreach (var personId in population.GetAllPersonIds())
        {
            var person = population.GetPerson(personId);
            if (!person.PersonalAccountId.IsNone)
            {
                if (!ledger.AccountExists(person.PersonalAccountId))
                {
                    violations.Add($"Person {personId} personal account {person.PersonalAccountId} missing in ledger.");
                }
                else if (ledger.GetOwner(person.PersonalAccountId) != person.Id.EntityId)
                {
                    violations.Add($"Person {personId} personal account {person.PersonalAccountId} owned by {ledger.GetOwner(person.PersonalAccountId)}, expected {person.Id.EntityId}.");
                }
            }
        }

        foreach (var householdId in population.GetAllHouseholdIds())
        {
            var household = population.GetHousehold(householdId);
            if (household is not null)
            {
                if (!ledger.AccountExists(household.AccountId))
                {
                    violations.Add($"Household {householdId} account {household.AccountId} missing in ledger.");
                }
                else if (ledger.GetOwner(household.AccountId) != household.Id.EntityId)
                {
                    violations.Add($"Household {householdId} account {household.AccountId} owned by {ledger.GetOwner(household.AccountId)}, expected {household.Id.EntityId}.");
                }
            }
        }

        foreach (var companyId in companies.GetAllCompanyIds())
        {
            var company = companies.GetCompany(companyId);
            if (company is not null)
            {
                if (!ledger.AccountExists(company.TreasuryAccountId))
                {
                    violations.Add($"Company {companyId} treasury account {company.TreasuryAccountId} missing in ledger.");
                }
                else if (ledger.GetOwner(company.TreasuryAccountId) != company.Id.EntityId)
                {
                    violations.Add($"Company {companyId} treasury account {company.TreasuryAccountId} owned by {ledger.GetOwner(company.TreasuryAccountId)}, expected {company.Id.EntityId}.");
                }
            }
        }

        // Position occupancy checks
        foreach (var positionId in companies.GetAllPositionIds())
        {
            var occupyingContractId = employment.GetOccupyingContractId(positionId);
            if (!occupyingContractId.IsNone)
            {
                var contract = employment.GetContract(occupyingContractId);
                if (contract.IsNone || !contract.IsActive || contract.JobPositionId != positionId)
                {
                    violations.Add($"Position {positionId} occupied by invalid contract {occupyingContractId}.");
                }
            }
        }

        // Payroll validity checks
        foreach (var contractId in employment.GetAllContractIds())
        {
            var payroll = employment.GetPayrollState(contractId);
            if (payroll is null)
            {
                violations.Add($"Contract {contractId} has no payroll state.");
            }
            else
            {
                if (payroll.AccruedUnpaidWagesMinorUnits < 0)
                {
                    violations.Add($"Contract {contractId} has negative unpaid wages: {payroll.AccruedUnpaidWagesMinorUnits}.");
                }
                if (payroll.ConsecutiveMissedPayPeriods < 0)
                {
                    violations.Add($"Contract {contractId} has negative missed pay periods: {payroll.ConsecutiveMissedPayPeriods}.");
                }
            }
        }

        return (violations.Count == 0, violations);
    }

    /// <summary>
    /// Gets a summary of the current simulation state.
    /// </summary>
    public Phase3Summary GetSummary()
    {
        var population = Engine.State.Population;
        var ledger = Engine.State.MoneyLedger;
        var companies = Engine.State.Companies;
        var employment = Engine.State.Employment;

        long totalHouseholdSharedBalance = 0;
        int totalPersons = 0;

        foreach (var householdId in population.GetAllHouseholdIds())
        {
            var household = population.GetHousehold(householdId);
            if (household is null) continue;

            var balance = ledger.GetBalance(household.AccountId);
            totalHouseholdSharedBalance += balance.MinorUnits;
            totalPersons += household.MemberCount;
        }

        long totalPersonalBalance = 0;
        foreach (var personId in population.GetAllPersonIds())
        {
            var person = population.GetPerson(personId);
            if (!person.PersonalAccountId.IsNone)
            {
                totalPersonalBalance += ledger.GetBalance(person.PersonalAccountId).MinorUnits;
            }
        }

        long totalTreasuryBalance = 0;
        foreach (var companyId in companies.GetAllCompanyIds())
        {
            var company = companies.GetCompany(companyId);
            if (company is null) continue;

            var balance = ledger.GetBalance(company.TreasuryAccountId);
            totalTreasuryBalance += balance.MinorUnits;
        }

        long totalUnpaidWages = 0;
        int contractsInArrears = 0;
        int terminatedContracts = 0;
        foreach (var contractId in employment.GetAllContractIds())
        {
            var contract = employment.GetContract(contractId);
            if (!contract.IsActive)
            {
                terminatedContracts++;
            }

            var payroll = employment.GetPayrollState(contractId);
            if (payroll is not null && payroll.AccruedUnpaidWagesMinorUnits > 0)
            {
                totalUnpaidWages += payroll.AccruedUnpaidWagesMinorUnits;
                contractsInArrears++;
            }
        }

        long aggregateCohortPopulation = 0;
        foreach (var cohortId in population.GetAllCohortIds())
        {
            var cohort = population.GetCohort(cohortId);
            aggregateCohortPopulation += cohort.Count;
        }

        var (integrityValid, integrityViolations) = ValidateIntegrity();

        return new Phase3Summary
        {
            TotalHouseholds = population.HouseholdCount,
            DetailedPersons = population.IndividualCount,
            AggregatePopulation = aggregateCohortPopulation,
            TotalPersons = totalPersons,
            TotalCompanies = companies.CompanyCount,
            TotalJobPositions = companies.PositionCount,
            TotalContracts = employment.ContractCount,
            ActiveContracts = employment.ActiveContractCount,
            TerminatedContracts = terminatedContracts,
            TotalHouseholdSharedBalance = new Money(totalHouseholdSharedBalance, Currency),
            TotalPersonalBalance = new Money(totalPersonalBalance, Currency),
            TotalCompanyTreasury = new Money(totalTreasuryBalance, Currency),
            TotalLedgerBalance = ledger.GetTotalBalance(),
            TotalExternalCredits = ledger.TotalExternalCredits,
            TotalExternalDebits = ledger.TotalExternalDebits,
            TotalUnpaidWages = new Money(totalUnpaidWages, Currency),
            ContractsInArrears = contractsInArrears,
            SupplyAccountingValid = ledger.VerifySupplyAccounting(),
            IntegrityValid = integrityValid,
            IntegrityViolations = integrityViolations,
            CanonicalFingerprint = SimulationStateFingerprint.Compute(Engine),
        };
    }
}

/// <summary>
/// Summary statistics for Phase 3 scenario.
/// </summary>
public sealed record Phase3Summary
{
    public required int TotalHouseholds { get; init; }
    public required int DetailedPersons { get; init; }
    public required long AggregatePopulation { get; init; }
    public required int TotalPersons { get; init; }
    public required int TotalCompanies { get; init; }
    public required int TotalJobPositions { get; init; }
    public required int TotalContracts { get; init; }
    public required int ActiveContracts { get; init; }
    public required int TerminatedContracts { get; init; }
    public required Money TotalHouseholdSharedBalance { get; init; }
    public required Money TotalPersonalBalance { get; init; }
    public required Money TotalCompanyTreasury { get; init; }
    public required Money TotalLedgerBalance { get; init; }
    public required Money TotalExternalCredits { get; init; }
    public required Money TotalExternalDebits { get; init; }
    public required Money TotalUnpaidWages { get; init; }
    public required int ContractsInArrears { get; init; }
    public required bool SupplyAccountingValid { get; init; }
    public required bool IntegrityValid { get; init; }
    public required IReadOnlyList<string> IntegrityViolations { get; init; }
    public required string CanonicalFingerprint { get; init; }

    /// <summary>
    /// Backward-compatibility property representing household shared balance.
    /// </summary>
    public Money TotalHouseholdMoneySupply => TotalHouseholdSharedBalance;
}
