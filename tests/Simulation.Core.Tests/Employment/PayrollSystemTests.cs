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
}
