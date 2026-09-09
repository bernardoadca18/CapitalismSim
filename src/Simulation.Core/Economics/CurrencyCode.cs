namespace CapitalismSim.Simulation.Core.Economics;

/// <summary>
/// Represents an ISO 4217-style currency code (e.g., USD, EUR, BRL).
/// Stored as a 3-character uppercase string for simplicity and interoperability.
/// </summary>
/// <remarks>
/// This phase defines only the value type; no exchange rates or multi-currency
/// operations are implemented. Future phases may add currency conversion when
/// multiple jurisdictions are introduced.
/// </remarks>
public readonly struct CurrencyCode : IEquatable<CurrencyCode>
{
    public string Code { get; }

    public CurrencyCode(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        if (code.Length != 3)
        {
            throw new ArgumentException(
                $"Currency code must be exactly 3 characters, got '{code}'.",
                nameof(code));
        }

        foreach (char c in code)
        {
            if (c < 'A' || c > 'Z')
            {
                throw new ArgumentException(
                    $"Currency code must contain only uppercase ASCII letters A-Z, got '{code}'.",
                    nameof(code));
            }
        }

        Code = code;
    }

    /// <summary>Sentinel value representing an unset/invalid currency.</summary>
    public static CurrencyCode None => default;

    public static readonly CurrencyCode USD = new("USD");
    public static readonly CurrencyCode EUR = new("EUR");
    public static readonly CurrencyCode BRL = new("BRL");

    public bool Equals(CurrencyCode other) => Code == other.Code;

    public override bool Equals(object? obj) => obj is CurrencyCode other && Equals(other);

    public override int GetHashCode() => Code.GetHashCode();

    public static bool operator ==(CurrencyCode left, CurrencyCode right) => left.Equals(right);

    public static bool operator !=(CurrencyCode left, CurrencyCode right) => !left.Equals(right);

    public override string ToString() => Code;
}
