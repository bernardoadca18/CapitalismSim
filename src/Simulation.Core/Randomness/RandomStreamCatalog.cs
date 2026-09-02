namespace CapitalismSim.Simulation.Core.Randomness;

/// <summary>
/// Derives independent, deterministic <see cref="DeterministicRandom"/>
/// streams from a single world seed, so that unrelated systems never
/// accidentally share or perturb each other's random sequences.
/// </summary>
/// <remarks>
/// <para>
/// The motivating scenario (see the project brief) is: adding a new random
/// draw inside a future real-estate system must never change the sequence
/// of numbers a future population-generation system consumes, even though
/// both ultimately derive from the same world seed. This is achieved by
/// hashing the world seed together with a stable domain name, an optional
/// entity identity, and an optional purpose string into a distinct PCG
/// stream selector for every distinct combination.
/// </para>
/// <para>
/// No concrete domains exist yet in this phase — this is purely the
/// generic derivation infrastructure. See <c>docs/SIMULATION_MODEL.md</c>.
/// </para>
/// </remarks>
public sealed class RandomStreamCatalog
{
    private readonly ulong _rootSeed;

    public RandomStreamCatalog(ulong rootSeed)
    {
        _rootSeed = rootSeed;
    }

    /// <summary>The root seed all streams in this catalog are derived from.</summary>
    public ulong RootSeed => _rootSeed;

    /// <summary>
    /// Derives an independent stream for a domain, with no specific entity
    /// or purpose qualifier.
    /// </summary>
    public DeterministicRandom GetStream(RandomDomain domain)
        => GetStream(domain, entityIdentity: null, purpose: null);

    /// <summary>
    /// Derives an independent stream for a domain and a specific purpose
    /// within it (for example, domain "RealEstate", purpose "PriceJitter").
    /// </summary>
    public DeterministicRandom GetStream(RandomDomain domain, string? purpose)
        => GetStream(domain, entityIdentity: null, purpose);

    /// <summary>
    /// Derives an independent stream scoped to a specific entity within a
    /// domain (for example, a specific future company deciding whether to
    /// lay off workers), optionally further scoped by purpose.
    /// </summary>
    public DeterministicRandom GetStream(RandomDomain domain, long? entityIdentity, string? purpose)
    {
        ulong hash = StableHash.ComputeUInt64(domain.Name);
        hash = StableHash.Combine(hash, unchecked((ulong)_rootSeed));

        if (entityIdentity is long entity)
        {
            hash = StableHash.Combine(hash, unchecked((ulong)entity));
        }

        if (!string.IsNullOrEmpty(purpose))
        {
            hash = StableHash.Combine(hash, StableHash.ComputeUInt64(purpose));
        }

        // Split the combined hash into a seed and a stream selector so that
        // two derivations that happen to share one half never collide.
        ulong seed = hash;
        ulong selector = StableHash.Combine(hash, 0x9E3779B97F4A7C15UL);

        return new DeterministicRandom(seed, selector);
    }
}
