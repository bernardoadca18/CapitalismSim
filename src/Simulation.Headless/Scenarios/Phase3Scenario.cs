using CapitalismSim.Simulation.Core.Companies;
using CapitalismSim.Simulation.Core.Economics;
using CapitalismSim.Simulation.Core.Entities;
using CapitalismSim.Simulation.Core.Engine;
using CapitalismSim.Simulation.Core.Employment;
using CapitalismSim.Simulation.Core.Population;
using CapitalismSim.Simulation.Core.Scheduling;
using CapitalismSim.Simulation.Core.Systems;
using CapitalismSim.Simulation.Core.Time;

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
                var compensation = new CompensationTerms(payAmount, 30); // 30-day pay interval

                var position = new JobPosition(positionId, companyId, JobRoleCode.GeneralWorker, compensation);
                companies.AddPosition(position);

                allPositions.Add((companyId, positionId, compensation));
            }
        }

        // Hire persons for available positions
        int personIndex = 0;
        foreach (var (companyId, positionId, compensation) in allPositions)
        {
            if (personIndex >= allPersons.Count)
                break;

            var personId = allPersons[personIndex++];

            // Create employment contract
            var contractEntityId = engine.EntityIds.Allocate();
            var contractId = new EmploymentContractId(contractEntityId);

            var contract = new EmploymentContract(
                contractId,
                personId,
                companyId,
                positionId,
                startDayIndex: 0, // Start at epoch
                compensation);

            employment.CreateContract(contract, population, companies);
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
    /// Gets a summary of the current simulation state.
    /// </summary>
    public Phase3Summary GetSummary()
    {
        var population = Engine.State.Population;
        var ledger = Engine.State.MoneyLedger;
        var companies = Engine.State.Companies;
        var employment = Engine.State.Employment;

        long totalHouseholdBalance = 0;
        int totalPersons = 0;

        foreach (var householdId in population.GetAllHouseholdIds())
        {
            var household = population.GetHousehold(householdId);
            if (household is null) continue;

            var balance = ledger.GetBalance(household.AccountId);
            totalHouseholdBalance += balance.MinorUnits;
            totalPersons += household.MemberCount;
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
        foreach (var contractId in employment.GetAllContractIds())
        {
            var payroll = employment.GetPayrollState(contractId);
            if (payroll is not null && payroll.AccruedUnpaidWagesMinorUnits > 0)
            {
                totalUnpaidWages += payroll.AccruedUnpaidWagesMinorUnits;
                contractsInArrears++;
            }
        }

        return new Phase3Summary
        {
            TotalHouseholds = population.HouseholdCount,
            TotalPersons = totalPersons,
            TotalCompanies = companies.CompanyCount,
            TotalJobPositions = companies.PositionCount,
            TotalContracts = employment.ContractCount,
            ActiveContracts = employment.ActiveContractCount,
            TotalHouseholdMoneySupply = new Money(totalHouseholdBalance, Currency),
            TotalCompanyTreasury = new Money(totalTreasuryBalance, Currency),
            TotalUnpaidWages = new Money(totalUnpaidWages, Currency),
            ContractsInArrears = contractsInArrears,
            SupplyAccountingValid = ledger.VerifySupplyAccounting(),
        };
    }
}

/// <summary>
/// Summary statistics for Phase 3 scenario.
/// </summary>
public sealed record Phase3Summary
{
    public required int TotalHouseholds { get; init; }
    public required int TotalPersons { get; init; }
    public required int TotalCompanies { get; init; }
    public required int TotalJobPositions { get; init; }
    public required int TotalContracts { get; init; }
    public required int ActiveContracts { get; init; }
    public required Money TotalHouseholdMoneySupply { get; init; }
    public required Money TotalCompanyTreasury { get; init; }
    public required Money TotalUnpaidWages { get; init; }
    public required int ContractsInArrears { get; init; }
    public required bool SupplyAccountingValid { get; init; }
}
