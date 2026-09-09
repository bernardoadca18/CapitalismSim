using CapitalismSim.Simulation.Core.Entities;

namespace CapitalismSim.Simulation.Core.Employment;

/// <summary>
/// Tracks payroll state for an employment contract, including wage arrears.
/// </summary>
/// <remarks>
/// This class manages the accrual and payment of wages for an employment contract.
/// It tracks:
/// - Accrued unpaid wages (arrears)
/// - Consecutive missed pay periods (for detecting payment issues)
/// - Next pay day index (when payment is due)
///
/// Wage Arrears Design:
/// When a company cannot pay wages (insufficient treasury balance), the wages
/// are accrued as unpaid wages. This creates a liability for the company.
/// Future payroll runs will attempt to pay accrued wages before current wages.
///
/// Consecutive missed pay periods tracks how many pay periods in a row
/// the company failed to pay. This can trigger consequences in future phases
/// (employee dissatisfaction, legal action, etc.).
/// </remarks>
public sealed class PayrollState
{
    /// <summary>
    /// The contract this payroll state belongs to.
    /// </summary>
    public EmploymentContractId ContractId { get; }

    /// <summary>
    /// Wages that have been earned but not yet paid (in minor units).
    /// </summary>
    public long AccruedUnpaidWagesMinorUnits { get; private set; }

    /// <summary>
    /// Number of consecutive pay periods where payment was missed or incomplete.
    /// Reset to 0 when full payment is made.
    /// </summary>
    public int ConsecutiveMissedPayPeriods { get; private set; }

    /// <summary>
    /// The next simulation day when payment is due.
    /// </summary>
    public long NextPayDayIndex { get; private set; }

    /// <summary>
    /// Creates a new payroll state.
    /// </summary>
    /// <param name="contractId">The contract this state belongs to.</param>
    /// <param name="nextPayDayIndex">The first pay day for this contract.</param>
    public PayrollState(EmploymentContractId contractId, long nextPayDayIndex)
    {
        if (contractId.IsNone)
        {
            throw new ArgumentException("Contract ID cannot be None.", nameof(contractId));
        }

        if (nextPayDayIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nextPayDayIndex), "Next pay day index cannot be negative.");
        }

        ContractId = contractId;
        NextPayDayIndex = nextPayDayIndex;
        AccruedUnpaidWagesMinorUnits = 0;
        ConsecutiveMissedPayPeriods = 0;
    }

    /// <summary>
    /// Accrues wages that couldn't be paid.
    /// </summary>
    public void AccrueUnpaidWages(long amountMinorUnits)
    {
        if (amountMinorUnits < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amountMinorUnits), "Amount cannot be negative.");
        }

        AccruedUnpaidWagesMinorUnits = checked(AccruedUnpaidWagesMinorUnits + amountMinorUnits);
    }

    /// <summary>
    /// Pays down accrued wages. Returns the amount actually paid.
    /// </summary>
    public long PayAccruedWages(long amountMinorUnits)
    {
        if (amountMinorUnits < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amountMinorUnits), "Amount cannot be negative.");
        }

        long paid = Math.Min(AccruedUnpaidWagesMinorUnits, amountMinorUnits);
        AccruedUnpaidWagesMinorUnits -= paid;
        return paid;
    }

    /// <summary>
    /// Records a missed pay period (company couldn't pay).
    /// </summary>
    public void RecordMissedPayPeriod()
    {
        checked
        {
            ConsecutiveMissedPayPeriods++;
        }
    }

    /// <summary>
    /// Resets the consecutive missed pay periods counter (full payment made).
    /// </summary>
    public void ResetMissedPayPeriods()
    {
        ConsecutiveMissedPayPeriods = 0;
    }

    /// <summary>
    /// Advances the next pay day by the specified interval.
    /// </summary>
    public void AdvanceNextPayDay(int intervalDays)
    {
        if (intervalDays <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(intervalDays), "Interval must be positive.");
        }

        checked
        {
            NextPayDayIndex += intervalDays;
        }
    }

    /// <summary>
    /// Creates a deep copy of this payroll state.
    /// </summary>
    public PayrollState Clone()
    {
        var clone = new PayrollState(ContractId, NextPayDayIndex);
        clone.Restore(AccruedUnpaidWagesMinorUnits, ConsecutiveMissedPayPeriods, NextPayDayIndex);
        return clone;
    }

    /// <summary>
    /// Restores payroll state from snapshot data.
    /// </summary>
    internal void Restore(long accruedUnpaidWagesMinorUnits, int consecutiveMissedPayPeriods, long nextPayDayIndex)
    {
        AccruedUnpaidWagesMinorUnits = accruedUnpaidWagesMinorUnits;
        ConsecutiveMissedPayPeriods = consecutiveMissedPayPeriods;
        NextPayDayIndex = nextPayDayIndex;
    }
}
