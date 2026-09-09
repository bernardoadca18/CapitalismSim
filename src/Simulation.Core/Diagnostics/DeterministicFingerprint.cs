using CapitalismSim.Simulation.Core.Randomness;

namespace CapitalismSim.Simulation.Core.Diagnostics;

/// <summary>
/// Computes a stable 64-bit fingerprint of a sequence of values that
/// together describe the observable state of a simulation run. This exists
/// so tests (and the headless runner's <c>--verify-determinism</c> option)
/// can assert "these two runs produced the exact same result" without
/// comparing every field of every subsystem by hand.
/// </summary>
/// <remarks>
/// Built on <see cref="StableHash"/> rather than
/// <see cref="object.GetHashCode"/>, for the same persistence-stability
/// reasons documented there. Feed it any values that should be part of the
/// "identity" of a run — for example, the final clock instant, RNG draws
/// produced during the run, and counts of events/commands processed.
/// </remarks>
public sealed class DeterministicFingerprintBuilder
{
    private ulong _hash = 0xcbf29ce484222325UL; // FNV-1a 64-bit offset basis.

    public DeterministicFingerprintBuilder Add(long value)
    {
        _hash = StableHash.Combine(_hash, unchecked((ulong)value));
        return this;
    }

    public DeterministicFingerprintBuilder Add(ulong value)
    {
        _hash = StableHash.Combine(_hash, value);
        return this;
    }

    public DeterministicFingerprintBuilder Add(string value)
    {
        _hash = StableHash.Combine(_hash, StableHash.ComputeUInt64(value));
        return this;
    }

    public DeterministicFingerprintBuilder Add(int value)
    {
        return Add((long)value);
    }

    public DeterministicFingerprintBuilder Add(bool value)
    {
        return Add(value ? 1L : 0L);
    }

    /// <summary>Finalizes and returns the fingerprint as a 16-digit hex string.</summary>
    public string Build() => _hash.ToString("x16", System.Globalization.CultureInfo.InvariantCulture);
}
