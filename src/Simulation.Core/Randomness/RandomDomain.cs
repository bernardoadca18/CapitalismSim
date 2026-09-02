namespace CapitalismSim.Simulation.Core.Randomness;

/// <summary>
/// Identifies a named "domain" that owns an independent slice of the
/// simulation's randomness (for example, a future domain like
/// <c>"Population"</c> or <c>"RealEstate"</c>). Domains are simple stable
/// strings chosen by callers; this type exists only to make call sites
/// self-documenting and to avoid accidentally passing a purpose string
/// where a domain string was expected.
/// </summary>
/// <remarks>
/// No concrete domains are defined in this phase. Callers of
/// <see cref="RandomStreamCatalog"/> (such as future gameplay systems, or
/// the diagnostic probe system in this phase) define their own domain
/// names.
/// </remarks>
public readonly record struct RandomDomain
{
    public string Name { get; }

    public RandomDomain(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        Name = name;
    }

    public override string ToString() => Name;
}
