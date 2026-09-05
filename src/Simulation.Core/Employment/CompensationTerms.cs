using CapitalismSim.Simulation.Core.Economics;

namespace CapitalismSim.Simulation.Core.Employment;

/// <summary>
/// Represents the compensation terms for a job position or employment contract.
/// </summary>
/// <remarks>
/// This is a value type with currency semantics - compensation is expressed in
/// minor units (e.g., cents) with a pay interval in days.
///
/// Design invariants:
/// - Pay amount must be positive for real positions (0 only for None sentinel)
/// - Pay interval must be positive (e.g., 7 for weekly, 14 for bi-weekly, 30 for monthly)
/// - CompensationTerms.None represents an invalid/unset compensation
///
/// Compensation is copied from JobPosition to EmploymentContract at hire time,
/// allowing position changes to not affect existing contracts.
/// </remarks>
public readonly record struct CompensationTerms
{
    /// <summary>The pay amount per period in minor units (e.g., cents).</summary>
    public long PayAmountMinorUnits { get; }

    /// <summary>The pay interval in simulation days.</summary>
    public int PayIntervalDays { get; }

    /// <summary>
    /// Creates new compensation terms.
    /// </summary>
    /// <param name="payAmountMinorUnits">Pay amount per period in minor units.</param>
    /// <param name="payIntervalDays">Pay interval in days (e.g., 7 for weekly, 30 for monthly).</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Pay amount is negative or pay interval is not positive.
    /// </exception>
    public CompensationTerms(long payAmountMinorUnits, int payIntervalDays)
    {
        if (payAmountMinorUnits < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(payAmountMinorUnits),
                "Pay amount cannot be negative.");
        }

        if (payIntervalDays <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(payIntervalDays),
                "Pay interval must be positive.");
        }

        PayAmountMinorUnits = payAmountMinorUnits;
        PayIntervalDays = payIntervalDays;
    }

    /// <summary>Whether this represents an unset/invalid compensation.</summary>
    public bool IsNone => PayAmountMinorUnits == 0 && PayIntervalDays == 0;

    /// <summary>Sentinel value representing unset compensation.</summary>
    public static CompensationTerms None => new(0, 0);

    /// <summary>Creates a zero-compensation placeholder with a valid interval.</summary>
    public static CompensationTerms Zero(int payIntervalDays) => new(0, payIntervalDays);

    /// <summary>
    /// Gets the pay amount as a Money struct for the given currency.
    /// </summary>
    public Money GetPayAmount(CurrencyCode currency) => new(PayAmountMinorUnits, currency);

    /// <summary>
    /// Calculates the daily pay rate for the given currency.
    /// </summary>
    public Money GetDailyRate(CurrencyCode currency) =>
        new(PayAmountMinorUnits / PayIntervalDays, currency);

    /// <summary>
    /// Validates that this compensation is suitable for a real position.
    /// Returns false for None sentinel or zero pay.
    /// </summary>
    public bool IsValidForPosition => PayAmountMinorUnits > 0 && PayIntervalDays > 0;

    public override string ToString()
    {
        if (IsNone) return "CompensationTerms(none)";
        return $"CompensationTerms({PayAmountMinorUnits} per {PayIntervalDays} days)";
    }
}
