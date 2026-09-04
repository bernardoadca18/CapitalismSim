namespace CapitalismSim.Simulation.Core.Economics;

/// <summary>
/// Represents a monetary amount in minor units (e.g., cents) with explicit currency.
/// Uses 64-bit signed integer arithmetic with overflow checking.
/// </summary>
/// <remarks>
/// This is deliberately not a floating-point type. Floating point accumulates
/// rounding error over long simulated durations (the project targets simulating
/// decades or centuries), which would eventually corrupt financial calculations.
/// Integer arithmetic with explicit overflow checking avoids both problems.
///
/// All amounts are stored in minor units (the smallest denomination of the currency,
/// e.g., cents for USD, centavos for BRL). The caller is responsible for formatting
/// for display purposes.
/// </remarks>
public readonly struct Money : IEquatable<Money>, IComparable<Money>
{
    public long MinorUnits { get; }
    public CurrencyCode Currency { get; }

    public Money(long minorUnits, CurrencyCode currency)
    {
        Currency = currency;
        MinorUnits = minorUnits;
    }

    public static Money Zero(CurrencyCode currency) => new(0, currency);

    public bool IsZero => MinorUnits == 0;

    public static Money operator +(Money left, Money right)
    {
        if (left.Currency != right.Currency)
        {
            throw new InvalidOperationException(
                $"Cannot add different currencies: {left.Currency} and {right.Currency}.");
        }

        return new Money(
            checked(left.MinorUnits + right.MinorUnits),
            left.Currency);
    }

    public static Money operator -(Money left, Money right)
    {
        if (left.Currency != right.Currency)
        {
            throw new InvalidOperationException(
                $"Cannot subtract different currencies: {left.Currency} and {right.Currency}.");
        }

        return new Money(
            checked(left.MinorUnits - right.MinorUnits),
            left.Currency);
    }

    public static Money operator -(Money value)
    {
        return new Money(checked(-value.MinorUnits), value.Currency);
    }

    public static Money operator *(Money money, int multiplier)
    {
        return new Money(checked(money.MinorUnits * multiplier), money.Currency);
    }

    public static Money operator *(int multiplier, Money money)
    {
        return new Money(checked(money.MinorUnits * multiplier), money.Currency);
    }

    public static bool operator ==(Money left, Money right) => left.Equals(right);

    public static bool operator !=(Money left, Money right) => !left.Equals(right);

    public static bool operator <(Money left, Money right)
    {
        ValidateSameCurrency(left, right);
        return left.MinorUnits < right.MinorUnits;
    }

    public static bool operator <=(Money left, Money right)
    {
        ValidateSameCurrency(left, right);
        return left.MinorUnits <= right.MinorUnits;
    }

    public static bool operator >(Money left, Money right)
    {
        ValidateSameCurrency(left, right);
        return left.MinorUnits > right.MinorUnits;
    }

    public static bool operator >=(Money left, Money right)
    {
        ValidateSameCurrency(left, right);
        return left.MinorUnits >= right.MinorUnits;
    }

    private static void ValidateSameCurrency(Money left, Money right)
    {
        if (left.Currency != right.Currency)
        {
            throw new InvalidOperationException(
                $"Cannot compare different currencies: {left.Currency} and {right.Currency}.");
        }
    }

    public bool Equals(Money other) => MinorUnits == other.MinorUnits && Currency == other.Currency;

    public override bool Equals(object? obj) => obj is Money other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(MinorUnits, Currency);

    public int CompareTo(Money other)
    {
        ValidateSameCurrency(this, other);
        return MinorUnits.CompareTo(other.MinorUnits);
    }

    public override string ToString()
    {
        return IsZero ? $"0 {Currency}" : $"{MinorUnits} {Currency}";
    }
}
