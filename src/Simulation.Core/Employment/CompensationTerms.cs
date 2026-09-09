using CapitalismSim.Simulation.Core.Economics;

namespace CapitalismSim.Simulation.Core.Employment;

/// <summary>
/// Represents the compensation terms for a job position or employment contract.
/// </summary>
/// <remarks>
/// This is a value type with currency semantics - compensation is expressed in
/// minor units (e.g., cents) with a pay interval in days and a currency code.
///
/// Design invariants:
/// - Pay amount must be non-negative
/// - Pay interval must be positive for valid compensation (e.g., 7 for weekly, 14 for bi-weekly, 30 for monthly)
/// - Currency must be a valid non-None currency code
/// - CompensationTerms.Unspecified represents an unset/invalid compensation (distinct from zero pay)
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

    /// <summary>The currency for the pay amount.</summary>
    public CurrencyCode Currency { get; }

    /// <summary>
    /// Creates new compensation terms.
    /// </summary>
    /// <param name="payAmountMinorUnits">Pay amount per period in minor units (must be >= 0).</param>
    /// <param name="payIntervalDays">Pay interval in days (must be > 0, e.g., 7 weekly, 30 monthly).</param>
    /// <param name="currency">The currency for the pay amount (must not be None).</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Pay amount is negative, pay interval is not positive, or currency is None.
    /// </exception>
    public CompensationTerms(long payAmountMinorUnits, int payIntervalDays, CurrencyCode currency)
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

        if (currency == CurrencyCode.None)
        {
            throw new ArgumentOutOfRangeException(nameof(currency),
                "Currency must not be None.");
        }

        PayAmountMinorUnits = payAmountMinorUnits;
        PayIntervalDays = payIntervalDays;
        Currency = currency;
    }

    /// <summary>Whether this represents an unset/invalid compensation.</summary>
    public bool IsUnspecified => PayIntervalDays == 0;

    /// <summary>Sentinel value representing unset compensation.</summary>
    public static CompensationTerms Unspecified => default;

    /// <summary>
    /// Gets the pay amount as a Money struct.
    /// </summary>
    public Money GetPayAmount() => new(PayAmountMinorUnits, Currency);

    /// <summary>
    /// Calculates the daily pay rate.
    /// </summary>
    public Money GetDailyRate() =>
        new(PayAmountMinorUnits / PayIntervalDays, Currency);

    /// <summary>
    /// Validates that this compensation is suitable for a real position.
    /// Returns false for Unspecified sentinel.
    /// </summary>
    public bool IsValidForPosition => !IsUnspecified;

    public override string ToString()
    {
        if (IsUnspecified) return "CompensationTerms(unspecified)";
        return $"CompensationTerms({PayAmountMinorUnits} {Currency} per {PayIntervalDays} days)";
    }
}
