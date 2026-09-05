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
        // Convert seconds to days (86400 seconds per day)
        var currentDayIndex = context.ScheduledInstant.TotalSeconds / 86400L;

        // Process each contract that might need payment today
        foreach (var contractId in employment.GetAllContractIds())
        {
            var contract = employment.GetContract(contractId);

            // Skip inactive contracts
            if (!contract.IsActive)
            {
                continue;
            }

            var payroll = employment.GetPayrollState(contractId);
            if (payroll is null)
            {
                continue;
            }

            // Check if today is a pay day for this contract
            if (payroll.NextPayDayIndex != currentDayIndex)
            {
                continue;
            }

            // Process payroll for this contract
            ProcessPayrollForContract(contract, payroll, companies, population, ledger);
        }
    }

    private static void ProcessPayrollForContract(
        EmploymentContract contract,
        PayrollState payroll,
        CompaniesState companies,
        PopulationState population,
        MoneyLedger ledger)
    {
        var company = companies.GetCompany(contract.CompanyId);
        if (company is null)
        {
            // Company doesn't exist - record missed payment
            payroll.AccrueUnpaidWages(contract.PayAmountMinorUnits);
            payroll.RecordMissedPayPeriod();
            payroll.AdvanceNextPayDay(contract.PayIntervalDays);
            return;
        }

        // Get the employee's personal account
        var person = population.GetPerson(contract.PersonId);
        if (person.IsNone)
        {
            // Person doesn't exist - record missed payment
            payroll.AccrueUnpaidWages(contract.PayAmountMinorUnits);
            payroll.RecordMissedPayPeriod();
            payroll.AdvanceNextPayDay(contract.PayIntervalDays);
            return;
        }

        var treasuryAccountId = company.TreasuryAccountId;
        var employeeAccountId = person.PersonalAccountId;
        var treasuryBalance = ledger.GetBalance(treasuryAccountId);
        var wageAmount = new Money(contract.PayAmountMinorUnits, treasuryBalance.Currency);

        // Calculate total owed: current wage + any accrued unpaid wages
        long totalOwedMinorUnits = checked(contract.PayAmountMinorUnits + payroll.AccruedUnpaidWagesMinorUnits);
        var totalOwed = new Money(totalOwedMinorUnits, treasuryBalance.Currency);

        // Determine how much can be paid
        Money amountToPay = treasuryBalance < totalOwed ? treasuryBalance : totalOwed;

        if (amountToPay.IsZero)
        {
            // Cannot pay anything
            payroll.AccrueUnpaidWages(contract.PayAmountMinorUnits);
            payroll.RecordMissedPayPeriod();
        }
        else
        {
            // Pay what we can
            // First, pay down accrued wages, then current wages
            long paidFromAccrued = payroll.PayAccruedWages(amountToPay.MinorUnits);
            long remainingToPay = amountToPay.MinorUnits - paidFromAccrued;

            // If there's remaining payment, it goes toward current wages
            if (remainingToPay > 0)
            {
                // Current wage was partially or fully paid
                if (remainingToPay < contract.PayAmountMinorUnits)
                {
                    // Partial payment - accrue the unpaid portion
                    payroll.AccrueUnpaidWages(contract.PayAmountMinorUnits - remainingToPay);
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
                // Payment only went toward accrued wages, not current
                payroll.AccrueUnpaidWages(contract.PayAmountMinorUnits);
                payroll.RecordMissedPayPeriod();
            }

            // Transfer money from company treasury to employee's personal account
            if (employeeAccountId.IsNone)
            {
                // Employee account doesn't exist - this shouldn't happen in valid state
                // Still record the transfer as external (money leaves company but goes nowhere)
                ledger.ExternalDebit(treasuryAccountId, amountToPay, MoneyFlowReason.Wages);
            }
            else
            {
                ledger.Transfer(treasuryAccountId, employeeAccountId, amountToPay);
            }
        }

        // Advance to next pay day
        payroll.AdvanceNextPayDay(contract.PayIntervalDays);
    }
}
