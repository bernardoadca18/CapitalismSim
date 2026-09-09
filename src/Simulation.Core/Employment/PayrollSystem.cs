using CapitalismSim.Simulation.Core.Companies;
using CapitalismSim.Simulation.Core.Economics;
using CapitalismSim.Simulation.Core.Entities;
using CapitalismSim.Simulation.Core.Population;
using CapitalismSim.Simulation.Core.Scheduling;
using CapitalismSim.Simulation.Core.State;
using CapitalismSim.Simulation.Core.Time;

namespace CapitalismSim.Simulation.Core.Employment;

/// <summary>
/// A simulation system that processes payroll on a daily basis.
/// Each tick, it checks for active employment contracts whose pay day has arrived
/// and attempts to pay wages from company treasuries to employee accounts.
/// </summary>
/// <remarks>
/// <para>
/// This system implements the wage payment model for Phase 3.
/// Companies pay employees according to their compensation terms.
/// If a company cannot pay (insufficient treasury balance), wages accrue as unpaid.
/// </para>
/// <para>
/// The system runs on a daily cadence. On each tick:
/// 1. For each active contract, check if today is a pay day
/// 2. If so, attempt to pay the wage amount from company treasury to employee account
/// 3. If payment succeeds, reset missed pay periods counter
/// 4. If payment fails (insufficient funds), accrue unpaid wages and increment missed periods
/// 5. Advance the next pay day by the pay interval
/// </para>
/// <para>
/// Future phases will add:
/// - Tax withholding
/// - Benefits deductions
/// - Overtime calculations
/// - Labor law enforcement (consequences for missed payments)
/// </para>
/// </remarks>
public sealed class PayrollSystem : ISimulationSystem
{
    public SimulationSystemId Id { get; }

    /// <summary>
    /// Creates a new PayrollSystem.
    /// </summary>
    public PayrollSystem()
    {
        Id = new SimulationSystemId("payroll");
    }

    /// <summary>
    /// Processes payroll for one day. Called by the scheduler.
    /// </summary>
    public void Tick(in SimulationTickContext context)
    {
        var state = context.State;
        var employment = state.Employment;
        var companies = state.Companies;
        var population = state.Population;
        var ledger = state.MoneyLedger;
        var currentDayIndex = context.ScheduledInstant.TotalSeconds / 86400L;

        // Process contracts in explicit ascending EmploymentContractId order for deterministic resolution
        // when treasury balance is insufficient to pay all employees.
        var contractIds = employment.GetAllContractIds()
            .OrderBy(id => id.EntityId.Value)
            .ToList();

        foreach (var contractId in contractIds)
        {
            var contract = employment.GetContract(contractId);
            if (contract.IsNone)
            {
                continue;
            }

            var payroll = employment.GetPayrollState(contractId);
            if (payroll is null)
            {
                continue;
            }

            if (contract.IsActive)
            {
                if (payroll.NextPayDayIndex == currentDayIndex)
                {
                    ProcessActivePayroll(contract, payroll, companies, population, ledger);
                }
            }
            else
            {
                // Terminated contract: attempt arrears-only repayment if arrears exist
                if (payroll.AccruedUnpaidWagesMinorUnits > 0)
                {
                    ProcessArrearsRepaymentForTerminatedContract(contract, payroll, companies, population, ledger);
                }
            }
        }
    }

    private static void ProcessActivePayroll(
        EmploymentContract contract,
        PayrollState payroll,
        CompaniesState companies,
        PopulationState population,
        MoneyLedger ledger)
    {
        // Fail-fast invariant validations: structural corruptions are not treated as insolvency
        var company = companies.GetCompany(contract.CompanyId);
        if (company is null)
        {
            throw new InvalidOperationException(
                $"Payroll invariant violation: Company {contract.CompanyId} for contract {contract.Id} does not exist.");
        }

        var person = population.GetPerson(contract.PersonId);
        if (person.IsNone)
        {
            throw new InvalidOperationException(
                $"Payroll invariant violation: Person {contract.PersonId} for contract {contract.Id} does not exist.");
        }

        var treasuryAccountId = company.TreasuryAccountId;
        if (treasuryAccountId.IsNone || !ledger.AccountExists(treasuryAccountId))
        {
            throw new InvalidOperationException(
                $"Payroll invariant violation: Company {company.Id} treasury account {treasuryAccountId} does not exist.");
        }

        if (ledger.GetOwner(treasuryAccountId) != company.Id.EntityId)
        {
            throw new InvalidOperationException(
                $"Payroll invariant violation: Company treasury account {treasuryAccountId} is not owned by company {company.Id}.");
        }

        var employeeAccountId = person.PersonalAccountId;
        if (employeeAccountId.IsNone || !ledger.AccountExists(employeeAccountId))
        {
            throw new InvalidOperationException(
                $"Payroll invariant violation: Employee {person.Id} personal account {employeeAccountId} does not exist.");
        }

        if (ledger.GetOwner(employeeAccountId) != person.Id.EntityId)
        {
            throw new InvalidOperationException(
                $"Payroll invariant violation: Employee personal account {employeeAccountId} is not owned by person {person.Id}.");
        }

        var treasuryBalance = ledger.GetBalance(treasuryAccountId);

        // Calculate total owed: current wage + accrued arrears
        long totalOwedMinorUnits = checked(contract.PayAmountMinorUnits + payroll.AccruedUnpaidWagesMinorUnits);
        var totalOwed = new Money(totalOwedMinorUnits, treasuryBalance.Currency);

        Money amountToPay = treasuryBalance < totalOwed ? treasuryBalance : totalOwed;

        if (amountToPay.IsZero)
        {
            // Legitimate insolvency: accrue unpaid wages and record missed pay period
            payroll.AccrueUnpaidWages(contract.PayAmountMinorUnits);
            payroll.RecordMissedPayPeriod();
        }
        else
        {
            // Pay down accrued wages first, then current wages
            long paidFromAccrued = payroll.PayAccruedWages(amountToPay.MinorUnits);
            long remainingToPay = checked(amountToPay.MinorUnits - paidFromAccrued);

            if (remainingToPay > 0)
            {
                if (remainingToPay < contract.PayAmountMinorUnits)
                {
                    // Partial payment of current wage
                    payroll.AccrueUnpaidWages(checked(contract.PayAmountMinorUnits - remainingToPay));
                    payroll.RecordMissedPayPeriod();
                }
                else
                {
                    // Full payment of current wages
                    payroll.ResetMissedPayPeriods();
                }
            }
            else
            {
                // Payment only went toward accrued wages
                payroll.AccrueUnpaidWages(contract.PayAmountMinorUnits);
                payroll.RecordMissedPayPeriod();
            }

            // Always transfer between accounts; never external debit/credit
            ledger.Transfer(treasuryAccountId, employeeAccountId, amountToPay);
        }

        // Advance next pay day for active contract
        payroll.AdvanceNextPayDay(contract.PayIntervalDays);
    }

    private static void ProcessArrearsRepaymentForTerminatedContract(
        EmploymentContract contract,
        PayrollState payroll,
        CompaniesState companies,
        PopulationState population,
        MoneyLedger ledger)
    {
        var company = companies.GetCompany(contract.CompanyId);
        if (company is null)
        {
            throw new InvalidOperationException(
                $"Payroll invariant violation: Company {contract.CompanyId} for terminated contract {contract.Id} does not exist.");
        }

        var person = population.GetPerson(contract.PersonId);
        if (person.IsNone)
        {
            throw new InvalidOperationException(
                $"Payroll invariant violation: Person {contract.PersonId} for terminated contract {contract.Id} does not exist.");
        }

        var treasuryAccountId = company.TreasuryAccountId;
        if (treasuryAccountId.IsNone || !ledger.AccountExists(treasuryAccountId))
        {
            throw new InvalidOperationException(
                $"Payroll invariant violation: Company {company.Id} treasury account {treasuryAccountId} does not exist.");
        }

        if (ledger.GetOwner(treasuryAccountId) != company.Id.EntityId)
        {
            throw new InvalidOperationException(
                $"Payroll invariant violation: Company treasury account {treasuryAccountId} is not owned by company {company.Id}.");
        }

        var employeeAccountId = person.PersonalAccountId;
        if (employeeAccountId.IsNone || !ledger.AccountExists(employeeAccountId))
        {
            throw new InvalidOperationException(
                $"Payroll invariant violation: Employee {person.Id} personal account {employeeAccountId} does not exist.");
        }

        if (ledger.GetOwner(employeeAccountId) != person.Id.EntityId)
        {
            throw new InvalidOperationException(
                $"Payroll invariant violation: Employee personal account {employeeAccountId} is not owned by person {person.Id}.");
        }

        var treasuryBalance = ledger.GetBalance(treasuryAccountId);
        if (treasuryBalance.IsZero)
        {
            return;
        }

        long arrearsOwed = payroll.AccruedUnpaidWagesMinorUnits;
        var owed = new Money(arrearsOwed, treasuryBalance.Currency);
        Money amountToPay = treasuryBalance < owed ? treasuryBalance : owed;

        if (!amountToPay.IsZero)
        {
            payroll.PayAccruedWages(amountToPay.MinorUnits);
            ledger.Transfer(treasuryAccountId, employeeAccountId, amountToPay);
        }
    }
}
