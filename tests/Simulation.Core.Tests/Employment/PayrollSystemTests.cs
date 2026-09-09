using CapitalismSim.Simulation.Core.Companies;
using CapitalismSim.Simulation.Core.Economics;
using CapitalismSim.Simulation.Core.Entities;
using CapitalismSim.Simulation.Core.Engine;
using CapitalismSim.Simulation.Core.Employment;
using CapitalismSim.Simulation.Core.Population;
using CapitalismSim.Simulation.Core.Scheduling;
using CapitalismSim.Simulation.Core.Time;
using Xunit;

namespace CapitalismSim.Simulation.Core.Tests.Employment;

public class PayrollSystemTests
{
    [Fact]
    public void Constructor_SetsId()
    {
        var system = new PayrollSystem();
        Assert.Equal(new SimulationSystemId("payroll"), system.Id);
    }

    [Fact]
    public void Tick_WithNoContracts_DoesNothing()
    {
        var engine = new SimulationEngine(new SimulationEngineOptions
        {
            WorldSeed = 12345,
            Currency = CurrencyCode.USD,
        });

        var system = new PayrollSystem();
        engine.Scheduler.Register(system, new SchedulingPolicy(
            SimulationInstant.Epoch,
            SimulationDuration.FromDays(1)));

        // Should not throw
        engine.AdvanceBy(SimulationDuration.FromDays(1));

        Assert.Equal(0, engine.State.Employment.ContractCount);
    }

    [Fact]
    public void Tick_WithInactiveContract_DoesNotPay()
    {
        var engine = new SimulationEngine(new SimulationEngineOptions
        {
            WorldSeed = 12345,
            Currency = CurrencyCode.USD,
        });

        // Create company
        var companyEntityId = engine.EntityIds.Allocate();
        var companyId = new CompanyId(companyEntityId);
        var treasuryAccountId = new AccountId(engine.EntityIds.Allocate());
        engine.State.MoneyLedger.CreateAccount(treasuryAccountId, companyEntityId);
        engine.State.MoneyLedger.ExternalCredit(treasuryAccountId, new Money(1000000, CurrencyCode.USD), MoneyFlowReason.InitialEndowment);

        var company = new Company(companyId, "Test Company", treasuryAccountId);
        engine.State.Companies.CreateCompany(company, engine.State.MoneyLedger);

        // Create position
        var positionId = new JobPositionId(engine.EntityIds.Allocate());
        var compensation = new CompensationTerms(50000, 30, CurrencyCode.USD);
        var position = new JobPosition(positionId, companyId, JobRoleCode.GeneralWorker, compensation);
        engine.State.Companies.AddPosition(position);

        // Create household and person
        var householdId = new HouseholdId(engine.EntityIds.Allocate());
        var householdAccountId = new AccountId(engine.EntityIds.Allocate());
        engine.State.Population.CreateHousehold(householdId, householdAccountId);
        engine.State.MoneyLedger.CreateAccount(householdAccountId, householdId.EntityId);

        var personId = new PersonId(engine.EntityIds.Allocate());
        var personalAccountId = new AccountId(engine.EntityIds.Allocate());
        engine.State.MoneyLedger.CreateAccount(personalAccountId, personId.EntityId);
        var person = Person.WithAge(personId, householdId, 30, 0, personalAccountId);
        engine.State.Population.AddPerson(person);

        // Create terminated contract
        var contractId = new EmploymentContractId(engine.EntityIds.Allocate());
        var contract = new EmploymentContract(
            contractId, personId, companyId, positionId,
            startDayIndex: 0, compensation).WithEndDayIndex(0); // Terminated at start

        engine.State.Employment.CreateContract(contract, engine.State.Population, engine.State.Companies);

        var system = new PayrollSystem();
        engine.Scheduler.Register(system, new SchedulingPolicy(
            SimulationInstant.Epoch,
            SimulationDuration.FromDays(1)));

        engine.AdvanceBy(SimulationDuration.FromDays(30));

        // Treasury should remain unchanged (no payments made)
        var treasuryBalance = engine.State.MoneyLedger.GetBalance(treasuryAccountId);
        Assert.Equal(1000000, treasuryBalance.MinorUnits);
    }

    [Fact]
    public void Tick_OnPayDay_WithSufficientFunds_TransfersWages()
    {
        var engine = new SimulationEngine(new SimulationEngineOptions
        {
            WorldSeed = 12345,
            Currency = CurrencyCode.USD,
        });

        // Create company with sufficient treasury
        var companyEntityId = engine.EntityIds.Allocate();
        var companyId = new CompanyId(companyEntityId);
        var treasuryAccountId = new AccountId(engine.EntityIds.Allocate());
        engine.State.MoneyLedger.CreateAccount(treasuryAccountId, companyEntityId);
        engine.State.MoneyLedger.ExternalCredit(treasuryAccountId, new Money(1000000, CurrencyCode.USD), MoneyFlowReason.InitialEndowment);

        var company = new Company(companyId, "Test Company", treasuryAccountId);
        engine.State.Companies.CreateCompany(company, engine.State.MoneyLedger);

        // Create position with $500/month pay
        var positionId = new JobPositionId(engine.EntityIds.Allocate());
        var compensation = new CompensationTerms(50000, 30, CurrencyCode.USD); // $500 every 30 days
        var position = new JobPosition(positionId, companyId, JobRoleCode.GeneralWorker, compensation);
        engine.State.Companies.AddPosition(position);

        // Create household and person
        var householdId = new HouseholdId(engine.EntityIds.Allocate());
        var householdAccountId = new AccountId(engine.EntityIds.Allocate());
        engine.State.Population.CreateHousehold(householdId, householdAccountId);
        engine.State.MoneyLedger.CreateAccount(householdAccountId, householdId.EntityId);

        var personId = new PersonId(engine.EntityIds.Allocate());
        var personalAccountId = new AccountId(engine.EntityIds.Allocate());
        engine.State.MoneyLedger.CreateAccount(personalAccountId, personId.EntityId);
        var person = Person.WithAge(personId, householdId, 30, 0, personalAccountId);
        engine.State.Population.AddPerson(person);

        // Create active contract starting at day 0
        var contractId = new EmploymentContractId(engine.EntityIds.Allocate());
        var contract = new EmploymentContract(contractId, personId, companyId, positionId, startDayIndex: 0, compensation);

        engine.State.Employment.CreateContract(contract, engine.State.Population, engine.State.Companies);

        var system = new PayrollSystem();
        engine.Scheduler.Register(system, new SchedulingPolicy(
            SimulationInstant.Epoch,
            SimulationDuration.FromDays(1)));

        // Advance 30 days to first pay day
        engine.AdvanceBy(SimulationDuration.FromDays(30));

        // Employee should have received wages
        var employeeBalance = engine.State.MoneyLedger.GetBalance(personalAccountId);
        Assert.Equal(50000, employeeBalance.MinorUnits);

        // Treasury should be reduced
        var treasuryBalance = engine.State.MoneyLedger.GetBalance(treasuryAccountId);
        Assert.Equal(950000, treasuryBalance.MinorUnits);

        // Supply accounting should remain valid
        Assert.True(engine.State.MoneyLedger.VerifySupplyAccounting());
    }

    [Fact]
    public void Tick_WithInsufficientFunds_AccruesUnpaidWages()
    {
        var engine = new SimulationEngine(new SimulationEngineOptions
        {
            WorldSeed = 12345,
            Currency = CurrencyCode.USD,
        });

        // Create company with insufficient treasury
        var companyEntityId = engine.EntityIds.Allocate();
        var companyId = new CompanyId(companyEntityId);
        var treasuryAccountId = new AccountId(engine.EntityIds.Allocate());
        engine.State.MoneyLedger.CreateAccount(treasuryAccountId, companyEntityId);
        engine.State.MoneyLedger.ExternalCredit(treasuryAccountId, new Money(10000, CurrencyCode.USD), MoneyFlowReason.InitialEndowment); // Only $100

        var company = new Company(companyId, "Test Company", treasuryAccountId);
        engine.State.Companies.CreateCompany(company, engine.State.MoneyLedger);

        // Create position with $500/month pay
        var positionId = new JobPositionId(engine.EntityIds.Allocate());
        var compensation = new CompensationTerms(50000, 30, CurrencyCode.USD); // $500 every 30 days
        var position = new JobPosition(positionId, companyId, JobRoleCode.GeneralWorker, compensation);
        engine.State.Companies.AddPosition(position);

        // Create household and person
        var householdId = new HouseholdId(engine.EntityIds.Allocate());
        var householdAccountId = new AccountId(engine.EntityIds.Allocate());
        engine.State.Population.CreateHousehold(householdId, householdAccountId);
        engine.State.MoneyLedger.CreateAccount(householdAccountId, householdId.EntityId);

        var personId = new PersonId(engine.EntityIds.Allocate());
        var personalAccountId = new AccountId(engine.EntityIds.Allocate());
        engine.State.MoneyLedger.CreateAccount(personalAccountId, personId.EntityId);
        var person = Person.WithAge(personId, householdId, 30, 0, personalAccountId);
        engine.State.Population.AddPerson(person);

        // Create contract
        var contractId = new EmploymentContractId(engine.EntityIds.Allocate());
        var contract = new EmploymentContract(contractId, personId, companyId, positionId, startDayIndex: 0, compensation);

        engine.State.Employment.CreateContract(contract, engine.State.Population, engine.State.Companies);

        var system = new PayrollSystem();
        engine.Scheduler.Register(system, new SchedulingPolicy(
            SimulationInstant.Epoch,
            SimulationDuration.FromDays(1)));

        // Advance 30 days to first pay day
        engine.AdvanceBy(SimulationDuration.FromDays(30));

        // Employee should have received partial payment (only $100 available)
        var employeeBalance = engine.State.MoneyLedger.GetBalance(personalAccountId);
        Assert.Equal(10000, employeeBalance.MinorUnits);

        // Check unpaid wages accrued
        var payroll = engine.State.Employment.GetPayrollState(contractId);
        Assert.NotNull(payroll);
        Assert.Equal(40000, payroll.AccruedUnpaidWagesMinorUnits); // $400 unpaid

        // Treasury should be empty
        var treasuryBalance = engine.State.MoneyLedger.GetBalance(treasuryAccountId);
        Assert.Equal(0, treasuryBalance.MinorUnits);
    }

    [Fact]
    public void Tick_PaysDownAccruedWagesFirst()
    {
        var engine = new SimulationEngine(new SimulationEngineOptions
        {
            WorldSeed = 12345,
            Currency = CurrencyCode.USD,
        });

        // Create company
        var companyEntityId = engine.EntityIds.Allocate();
        var companyId = new CompanyId(companyEntityId);
        var treasuryAccountId = new AccountId(engine.EntityIds.Allocate());
        engine.State.MoneyLedger.CreateAccount(treasuryAccountId, companyEntityId);
        engine.State.MoneyLedger.ExternalCredit(treasuryAccountId, new Money(10000, CurrencyCode.USD), MoneyFlowReason.InitialEndowment);

        var company = new Company(companyId, "Test Company", treasuryAccountId);
        engine.State.Companies.CreateCompany(company, engine.State.MoneyLedger);

        // Create position
        var positionId = new JobPositionId(engine.EntityIds.Allocate());
        var compensation = new CompensationTerms(50000, 30, CurrencyCode.USD);
        var position = new JobPosition(positionId, companyId, JobRoleCode.GeneralWorker, compensation);
        engine.State.Companies.AddPosition(position);

        // Create household and person
        var householdId = new HouseholdId(engine.EntityIds.Allocate());
        var householdAccountId = new AccountId(engine.EntityIds.Allocate());
        engine.State.Population.CreateHousehold(householdId, householdAccountId);
        engine.State.MoneyLedger.CreateAccount(householdAccountId, householdId.EntityId);

        var personId = new PersonId(engine.EntityIds.Allocate());
        var personalAccountId = new AccountId(engine.EntityIds.Allocate());
        engine.State.MoneyLedger.CreateAccount(personalAccountId, personId.EntityId);
        var person = Person.WithAge(personId, householdId, 30, 0, personalAccountId);
        engine.State.Population.AddPerson(person);

        // Create contract
        var contractId = new EmploymentContractId(engine.EntityIds.Allocate());
        var contract = new EmploymentContract(contractId, personId, companyId, positionId, startDayIndex: 0, compensation);

        engine.State.Employment.CreateContract(contract, engine.State.Population, engine.State.Companies);

        var system = new PayrollSystem();
        engine.Scheduler.Register(system, new SchedulingPolicy(
            SimulationInstant.Epoch,
            SimulationDuration.FromDays(1)));

        // First pay period - insufficient funds, accrues $400 unpaid
        engine.AdvanceBy(SimulationDuration.FromDays(30));

        var payroll = engine.State.Employment.GetPayrollState(contractId);
        Assert.NotNull(payroll);
        Assert.Equal(40000, payroll.AccruedUnpaidWagesMinorUnits);

        // Add more funds to treasury
        engine.State.MoneyLedger.ExternalCredit(treasuryAccountId, new Money(100000, CurrencyCode.USD), MoneyFlowReason.InitialEndowment);

        // Second pay period - should pay accrued + current
        engine.AdvanceBy(SimulationDuration.FromDays(30));

        // Total paid: $400 (accrued) + $500 (current) = $900
        var employeeBalance = engine.State.MoneyLedger.GetBalance(personalAccountId);
        Assert.Equal(10000 + 90000, employeeBalance.MinorUnits); // Initial $100 + $900

        payroll = engine.State.Employment.GetPayrollState(contractId);
        Assert.NotNull(payroll);
        Assert.Equal(0, payroll.AccruedUnpaidWagesMinorUnits);
    }

    [Fact]
    public void Tick_MaintainsSupplyAccounting()
    {
        var engine = new SimulationEngine(new SimulationEngineOptions
        {
            WorldSeed = 12345,
            Currency = CurrencyCode.USD,
        });

        // Create company
        var companyEntityId = engine.EntityIds.Allocate();
        var companyId = new CompanyId(companyEntityId);
        var treasuryAccountId = new AccountId(engine.EntityIds.Allocate());
        engine.State.MoneyLedger.CreateAccount(treasuryAccountId, companyEntityId);
        engine.State.MoneyLedger.ExternalCredit(treasuryAccountId, new Money(1000000, CurrencyCode.USD), MoneyFlowReason.InitialEndowment);

        var company = new Company(companyId, "Test Company", treasuryAccountId);
        engine.State.Companies.CreateCompany(company, engine.State.MoneyLedger);

        // Create position
        var positionId = new JobPositionId(engine.EntityIds.Allocate());
        var compensation = new CompensationTerms(50000, 30, CurrencyCode.USD);
        var position = new JobPosition(positionId, companyId, JobRoleCode.GeneralWorker, compensation);
        engine.State.Companies.AddPosition(position);

        // Create household and person
        var householdId = new HouseholdId(engine.EntityIds.Allocate());
        var householdAccountId = new AccountId(engine.EntityIds.Allocate());
        engine.State.Population.CreateHousehold(householdId, householdAccountId);
        engine.State.MoneyLedger.CreateAccount(householdAccountId, householdId.EntityId);

        var personId = new PersonId(engine.EntityIds.Allocate());
        var personalAccountId = new AccountId(engine.EntityIds.Allocate());
        engine.State.MoneyLedger.CreateAccount(personalAccountId, personId.EntityId);
        var person = Person.WithAge(personId, householdId, 30, 0, personalAccountId);
        engine.State.Population.AddPerson(person);

        // Create contract
        var contractId = new EmploymentContractId(engine.EntityIds.Allocate());
        var contract = new EmploymentContract(contractId, personId, companyId, positionId, startDayIndex: 0, compensation);

        engine.State.Employment.CreateContract(contract, engine.State.Population, engine.State.Companies);

        var system = new PayrollSystem();
        engine.Scheduler.Register(system, new SchedulingPolicy(
            SimulationInstant.Epoch,
            SimulationDuration.FromDays(1)));

        // Run for multiple pay periods
        engine.AdvanceBy(SimulationDuration.FromDays(90));

        // Supply accounting should remain valid throughout
        Assert.True(engine.State.MoneyLedger.VerifySupplyAccounting());
    }

    [Fact]
    public void Tick_WithMultipleContracts_PaysAll()
    {
        var engine = new SimulationEngine(new SimulationEngineOptions
        {
            WorldSeed = 12345,
            Currency = CurrencyCode.USD,
        });

        // Create company
        var companyEntityId = engine.EntityIds.Allocate();
        var companyId = new CompanyId(companyEntityId);
        var treasuryAccountId = new AccountId(engine.EntityIds.Allocate());
        engine.State.MoneyLedger.CreateAccount(treasuryAccountId, companyEntityId);
        engine.State.MoneyLedger.ExternalCredit(treasuryAccountId, new Money(5000000, CurrencyCode.USD), MoneyFlowReason.InitialEndowment);

        var company = new Company(companyId, "Test Company", treasuryAccountId);
        engine.State.Companies.CreateCompany(company, engine.State.MoneyLedger);

        // Create 3 positions
        var compensation = new CompensationTerms(50000, 30, CurrencyCode.USD);
        var positionIds = new JobPositionId[3];
        for (int i = 0; i < 3; i++)
        {
            var positionId = new JobPositionId(engine.EntityIds.Allocate());
            positionIds[i] = positionId;
            var position = new JobPosition(positionId, companyId, JobRoleCode.GeneralWorker, compensation);
            engine.State.Companies.AddPosition(position);
        }

        // Create 3 employees
        var personIds = new PersonId[3];
        var personalAccountIds = new AccountId[3];
        for (int i = 0; i < 3; i++)
        {
            var householdId = new HouseholdId(engine.EntityIds.Allocate());
            var householdAccountId = new AccountId(engine.EntityIds.Allocate());
            engine.State.Population.CreateHousehold(householdId, householdAccountId);
            engine.State.MoneyLedger.CreateAccount(householdAccountId, householdId.EntityId);

            var personId = new PersonId(engine.EntityIds.Allocate());
            personIds[i] = personId;
            var personalAccountId = new AccountId(engine.EntityIds.Allocate());
            personalAccountIds[i] = personalAccountId;
            engine.State.MoneyLedger.CreateAccount(personalAccountId, personId.EntityId);
            var person = Person.WithAge(personId, householdId, 30, 0, personalAccountId);
            engine.State.Population.AddPerson(person);

            // Create contract
            var contractId = new EmploymentContractId(engine.EntityIds.Allocate());
            var contract = new EmploymentContract(contractId, personId, companyId, positionIds[i], startDayIndex: 0, compensation);
            engine.State.Employment.CreateContract(contract, engine.State.Population, engine.State.Companies);
        }

        var system = new PayrollSystem();
        engine.Scheduler.Register(system, new SchedulingPolicy(
            SimulationInstant.Epoch,
            SimulationDuration.FromDays(1)));

        // Advance 30 days
        engine.AdvanceBy(SimulationDuration.FromDays(30));

        // All 3 employees should have been paid
        foreach (var accountId in personalAccountIds)
        {
            var balance = engine.State.MoneyLedger.GetBalance(accountId);
            Assert.Equal(50000, balance.MinorUnits);
        }

        // Treasury should be reduced by 3 * $500 = $1500
        var treasuryBalance = engine.State.MoneyLedger.GetBalance(treasuryAccountId);
        Assert.Equal(5000000 - 150000, treasuryBalance.MinorUnits);
    }

    [Fact]
    public void Tick_ResetsMissedPayPeriodsOnFullPayment()
    {
        var engine = new SimulationEngine(new SimulationEngineOptions
        {
            WorldSeed = 12345,
            Currency = CurrencyCode.USD,
        });

        // Create company with insufficient funds initially
        var companyEntityId = engine.EntityIds.Allocate();
        var companyId = new CompanyId(companyEntityId);
        var treasuryAccountId = new AccountId(engine.EntityIds.Allocate());
        engine.State.MoneyLedger.CreateAccount(treasuryAccountId, companyEntityId);
        engine.State.MoneyLedger.ExternalCredit(treasuryAccountId, new Money(10000, CurrencyCode.USD), MoneyFlowReason.InitialEndowment);

        var company = new Company(companyId, "Test Company", treasuryAccountId);
        engine.State.Companies.CreateCompany(company, engine.State.MoneyLedger);

        // Create position
        var positionId = new JobPositionId(engine.EntityIds.Allocate());
        var compensation = new CompensationTerms(50000, 30, CurrencyCode.USD);
        var position = new JobPosition(positionId, companyId, JobRoleCode.GeneralWorker, compensation);
        engine.State.Companies.AddPosition(position);

        // Create household and person
        var householdId = new HouseholdId(engine.EntityIds.Allocate());
        var householdAccountId = new AccountId(engine.EntityIds.Allocate());
        engine.State.Population.CreateHousehold(householdId, householdAccountId);
        engine.State.MoneyLedger.CreateAccount(householdAccountId, householdId.EntityId);

        var personId = new PersonId(engine.EntityIds.Allocate());
        var personalAccountId = new AccountId(engine.EntityIds.Allocate());
        engine.State.MoneyLedger.CreateAccount(personalAccountId, personId.EntityId);
        var person = Person.WithAge(personId, householdId, 30, 0, personalAccountId);
        engine.State.Population.AddPerson(person);

        // Create contract
        var contractId = new EmploymentContractId(engine.EntityIds.Allocate());
        var contract = new EmploymentContract(contractId, personId, companyId, positionId, startDayIndex: 0, compensation);

        engine.State.Employment.CreateContract(contract, engine.State.Population, engine.State.Companies);

        var system = new PayrollSystem();
        engine.Scheduler.Register(system, new SchedulingPolicy(
            SimulationInstant.Epoch,
            SimulationDuration.FromDays(1)));

        // First pay period - partial payment
        engine.AdvanceBy(SimulationDuration.FromDays(30));

        var payroll = engine.State.Employment.GetPayrollState(contractId);
        Assert.NotNull(payroll);
        Assert.Equal(1, payroll.ConsecutiveMissedPayPeriods);

        // Add sufficient funds
        engine.State.MoneyLedger.ExternalCredit(treasuryAccountId, new Money(1000000, CurrencyCode.USD), MoneyFlowReason.InitialEndowment);

        // Second pay period - full payment
        engine.AdvanceBy(SimulationDuration.FromDays(30));

        payroll = engine.State.Employment.GetPayrollState(contractId);
        Assert.NotNull(payroll);
        Assert.Equal(0, payroll.ConsecutiveMissedPayPeriods);
    }

    [Fact]
    public void Tick_DeterministicContractOrdering_WhenTreasuryIsInsufficient()
    {
        var engine = new SimulationEngine(new SimulationEngineOptions
        {
            WorldSeed = 12345,
            Currency = CurrencyCode.USD,
        });

        var companyEntityId = engine.EntityIds.Allocate();
        var companyId = new CompanyId(companyEntityId);
        var treasuryAccountId = new AccountId(engine.EntityIds.Allocate());
        engine.State.MoneyLedger.CreateAccount(treasuryAccountId, companyEntityId);
        // Only $500 in treasury, but each worker is owed $500
        engine.State.MoneyLedger.ExternalCredit(treasuryAccountId, new Money(50000, CurrencyCode.USD), MoneyFlowReason.InitialEndowment);
        var company = new Company(companyId, "Test Company", treasuryAccountId);
        engine.State.Companies.CreateCompany(company, engine.State.MoneyLedger);

        var comp = new CompensationTerms(50000, 30, CurrencyCode.USD);
        var pos1 = new JobPositionId(engine.EntityIds.Allocate());
        var pos2 = new JobPositionId(engine.EntityIds.Allocate());
        engine.State.Companies.AddPosition(new JobPosition(pos1, companyId, JobRoleCode.GeneralWorker, comp));
        engine.State.Companies.AddPosition(new JobPosition(pos2, companyId, JobRoleCode.GeneralWorker, comp));

        var hId = new HouseholdId(engine.EntityIds.Allocate());
        var hAcc = new AccountId(engine.EntityIds.Allocate());
        engine.State.Population.CreateHousehold(hId, hAcc);
        engine.State.MoneyLedger.CreateAccount(hAcc, hId.EntityId);

        var p1 = new PersonId(engine.EntityIds.Allocate());
        var p1Acc = new AccountId(engine.EntityIds.Allocate());
        engine.State.MoneyLedger.CreateAccount(p1Acc, p1.EntityId);
        engine.State.Population.AddPerson(Person.WithAge(p1, hId, 30, 0, p1Acc));

        var p2 = new PersonId(engine.EntityIds.Allocate());
        var p2Acc = new AccountId(engine.EntityIds.Allocate());
        engine.State.MoneyLedger.CreateAccount(p2Acc, p2.EntityId);
        engine.State.Population.AddPerson(Person.WithAge(p2, hId, 32, 0, p2Acc));

        var c1 = new EmploymentContractId(engine.EntityIds.Allocate());
        var c2 = new EmploymentContractId(engine.EntityIds.Allocate());
        Assert.True(c1.EntityId.Value < c2.EntityId.Value);

        engine.State.Employment.CreateContract(new EmploymentContract(c1, p1, companyId, pos1, 0, comp), engine.State.Population, engine.State.Companies);
        engine.State.Employment.CreateContract(new EmploymentContract(c2, p2, companyId, pos2, 0, comp), engine.State.Population, engine.State.Companies);

        var system = new PayrollSystem();
        engine.Scheduler.Register(system, new SchedulingPolicy(SimulationInstant.Epoch, SimulationDuration.FromDays(1)));

        engine.AdvanceBy(SimulationDuration.FromDays(30));

        // First contract receives the $500, second contract gets 0 and accrues arrears
        Assert.Equal(50000, engine.State.MoneyLedger.GetBalance(p1Acc).MinorUnits);
        Assert.Equal(0, engine.State.MoneyLedger.GetBalance(p2Acc).MinorUnits);
        Assert.Equal(0, engine.State.Employment.GetPayrollState(c1)!.AccruedUnpaidWagesMinorUnits);
        Assert.Equal(50000, engine.State.Employment.GetPayrollState(c2)!.AccruedUnpaidWagesMinorUnits);
    }

    [Fact]
    public void Tick_TerminatedContractWithArrears_RepaysArrearsWithoutNewAccrual()
    {
        var engine = new SimulationEngine(new SimulationEngineOptions
        {
            WorldSeed = 12345,
            Currency = CurrencyCode.USD,
        });

        var companyEntityId = engine.EntityIds.Allocate();
        var companyId = new CompanyId(companyEntityId);
        var treasuryAccountId = new AccountId(engine.EntityIds.Allocate());
        engine.State.MoneyLedger.CreateAccount(treasuryAccountId, companyEntityId);
        var company = new Company(companyId, "Test Company", treasuryAccountId);
        engine.State.Companies.CreateCompany(company, engine.State.MoneyLedger);

        var comp = new CompensationTerms(50000, 30, CurrencyCode.USD);
        var pos = new JobPositionId(engine.EntityIds.Allocate());
        engine.State.Companies.AddPosition(new JobPosition(pos, companyId, JobRoleCode.GeneralWorker, comp));

        var hId = new HouseholdId(engine.EntityIds.Allocate());
        var hAcc = new AccountId(engine.EntityIds.Allocate());
        engine.State.Population.CreateHousehold(hId, hAcc);
        engine.State.MoneyLedger.CreateAccount(hAcc, hId.EntityId);

        var p = new PersonId(engine.EntityIds.Allocate());
        var pAcc = new AccountId(engine.EntityIds.Allocate());
        engine.State.MoneyLedger.CreateAccount(pAcc, p.EntityId);
        engine.State.Population.AddPerson(Person.WithAge(p, hId, 30, 0, pAcc));

        var c = new EmploymentContractId(engine.EntityIds.Allocate());
        engine.State.Employment.CreateContract(new EmploymentContract(c, p, companyId, pos, 0, comp), engine.State.Population, engine.State.Companies);

        var system = new PayrollSystem();
        engine.Scheduler.Register(system, new SchedulingPolicy(SimulationInstant.Epoch, SimulationDuration.FromDays(1)));

        // Day 30: Company has 0 funds, worker misses pay and accrues $500
        engine.AdvanceBy(SimulationDuration.FromDays(30));
        var payroll = engine.State.Employment.GetPayrollState(c)!;
        Assert.Equal(50000, payroll.AccruedUnpaidWagesMinorUnits);

        // Day 35: Terminate contract
        engine.State.Employment.TerminateContract(c, 35);
        Assert.False(engine.State.Employment.GetContract(c).IsActive);

        // Day 40: Inject money into treasury
        engine.State.MoneyLedger.ExternalCredit(treasuryAccountId, new Money(50000, CurrencyCode.USD), MoneyFlowReason.InitialEndowment);

        // Advance past day 40: Terminated contract arrears repaid!
        engine.AdvanceBy(SimulationDuration.FromDays(10));
        Assert.Equal(0, payroll.AccruedUnpaidWagesMinorUnits);
        Assert.Equal(50000, engine.State.MoneyLedger.GetBalance(pAcc).MinorUnits);
        Assert.Equal(0, engine.State.MoneyLedger.GetBalance(treasuryAccountId).MinorUnits);
    }

    [Fact]
    public void Tick_PreservesMoneySupplyInvariant_AtAllTimes()
    {
        var engine = new SimulationEngine(new SimulationEngineOptions
        {
            WorldSeed = 12345,
            Currency = CurrencyCode.USD,
        });

        var companyEntityId = engine.EntityIds.Allocate();
        var companyId = new CompanyId(companyEntityId);
        var treasuryAccountId = new AccountId(engine.EntityIds.Allocate());
        engine.State.MoneyLedger.CreateAccount(treasuryAccountId, companyEntityId);
        engine.State.MoneyLedger.ExternalCredit(treasuryAccountId, new Money(100000, CurrencyCode.USD), MoneyFlowReason.InitialEndowment);
        var company = new Company(companyId, "Test Company", treasuryAccountId);
        engine.State.Companies.CreateCompany(company, engine.State.MoneyLedger);

        var comp = new CompensationTerms(30000, 10, CurrencyCode.USD);
        var pos = new JobPositionId(engine.EntityIds.Allocate());
        engine.State.Companies.AddPosition(new JobPosition(pos, companyId, JobRoleCode.GeneralWorker, comp));

        var hId = new HouseholdId(engine.EntityIds.Allocate());
        var hAcc = new AccountId(engine.EntityIds.Allocate());
        engine.State.Population.CreateHousehold(hId, hAcc);
        engine.State.MoneyLedger.CreateAccount(hAcc, hId.EntityId);

        var p = new PersonId(engine.EntityIds.Allocate());
        var pAcc = new AccountId(engine.EntityIds.Allocate());
        engine.State.MoneyLedger.CreateAccount(pAcc, p.EntityId);
        engine.State.Population.AddPerson(Person.WithAge(p, hId, 30, 0, pAcc));

        var c = new EmploymentContractId(engine.EntityIds.Allocate());
        engine.State.Employment.CreateContract(new EmploymentContract(c, p, companyId, pos, 0, comp), engine.State.Population, engine.State.Companies);

        var system = new PayrollSystem();
        engine.Scheduler.Register(system, new SchedulingPolicy(SimulationInstant.Epoch, SimulationDuration.FromDays(1)));

        var creditsBefore = engine.State.MoneyLedger.TotalExternalCredits;
        var debitsBefore = engine.State.MoneyLedger.TotalExternalDebits;

        engine.AdvanceBy(SimulationDuration.FromDays(100));

        Assert.Equal(creditsBefore, engine.State.MoneyLedger.TotalExternalCredits);
        Assert.Equal(debitsBefore, engine.State.MoneyLedger.TotalExternalDebits);
        Assert.True(engine.State.MoneyLedger.VerifySupplyAccounting());
    }
}
