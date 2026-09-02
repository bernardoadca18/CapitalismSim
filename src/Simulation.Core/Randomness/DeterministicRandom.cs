namespace CapitalismSim.Simulation.Core.Randomness;

/// <summary>
/// A small, explicitly specified, deterministic pseudo-random number
/// generator based on the PCG32 algorithm (permuted congruential generator,
/// XSH-RR variant, as described by M.E. O'Neill, 2014).
/// </summary>
/// <remarks>
/// <para>
/// This type is used instead of <see cref="System.Random"/> because the
/// .NET BCL does not guarantee the internal algorithm of
/// <see cref="System.Random"/> across runtimes or versions — it is not a
/// stable, persistable contract. PCG32 is small, public, well documented,
/// and its entire state fits in two <see langword="ulong"/> values, which
/// makes it trivial to serialize into a save file and restore exactly.
/// </para>
/// <para>
/// This is not a cryptographically secure generator and must never be used
/// for anything security-sensitive. It exists purely to drive reproducible
/// gameplay/simulation randomness.
/// </para>
/// </remarks>
public sealed class DeterministicRandom
{
    // The PCG "multiplier" constant recommended by the reference
    // implementation for the 64-bit state / 32-bit output variant.
    private const ulong Multiplier = 6364136223846793005UL;

    private ulong _state;
    private readonly ulong _increment;

    /// <summary>
    /// Creates a generator from a 64-bit seed and an odd "stream selector".
    /// Two generators with the same seed but different stream selectors
    /// produce different, independent-looking sequences.
    /// </summary>
    public DeterministicRandom(ulong seed, ulong streamSelector)
    {
        // The increment must be odd; PCG's construction guarantees this by
        // shifting the selector left by one bit and setting the low bit.
        _increment = (streamSelector << 1) | 1UL;
        _state = 0UL;
        NextUInt32(); // Discard: mixes the seed into state per PCG's init step.
        _state = unchecked(_state + seed);
        NextUInt32();
    }

    /// <summary>
    /// Restores a generator directly from previously captured state. Used
    /// exclusively by the persistence layer to resume a sequence exactly
    /// where it left off.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The increment is even. PCG requires an odd increment for correct operation.
    /// </exception>
    public static DeterministicRandom FromState(ulong state, ulong increment)
    {
        if ((increment & 1UL) == 0UL)
        {
            throw new ArgumentException(
                "Increment must be odd. PCG's construction requires an odd increment for correct operation.",
                nameof(increment));
        }

        return new DeterministicRandom(state, increment, isRawState: true);
    }

    private DeterministicRandom(ulong state, ulong increment, bool isRawState)
    {
        _ = isRawState;
        _state = state;
        _increment = increment;
    }

    /// <summary>The raw internal state, for snapshotting.</summary>
    public ulong State => _state;

    /// <summary>The raw stream increment, for snapshotting.</summary>
    public ulong Increment => _increment;

    /// <summary>Returns the next pseudo-random 32-bit unsigned integer.</summary>
    public uint NextUInt32()
    {
        ulong previous = _state;
        _state = unchecked(previous * Multiplier + _increment);

        uint xorShifted = (uint)(((previous >> 18) ^ previous) >> 27);
        int rotation = (int)(previous >> 59);

        return RotateRight(xorShifted, rotation);
    }

    /// <summary>Returns the next pseudo-random 64-bit unsigned integer.</summary>
    public ulong NextUInt64()
    {
        ulong high = NextUInt32();
        ulong low = NextUInt32();
        return (high << 32) | low;
    }

    /// <summary>
    /// Returns a pseudo-random integer in the range [<paramref name="minInclusive"/>, <paramref name="maxExclusive"/>).
    /// </summary>
    public int NextInt32(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxExclusive),
                maxExclusive,
                "maxExclusive must be greater than minInclusive.");
        }

        // Handle the full int range correctly by using unsigned arithmetic.
        // When minInclusive is negative and maxExclusive is positive,
        // the difference can exceed int.MaxValue and would overflow if cast to uint directly.
        // We compute the range as an unsigned value that correctly represents the span.
        ulong range = (ulong)(maxExclusive - minInclusive);

        if (range <= uint.MaxValue)
        {
            // Fast path: range fits in 32 bits
            uint bounded = NextBoundedUInt32((uint)range);
            return minInclusive + (int)bounded;
        }

        // Slow path: full int range [int.MinValue, int.MaxValue)
        // This is extremely rare in practice but must be correct.
        ulong sample = NextBoundedUInt64(range);
        return minInclusive + (int)sample;
    }

    /// <summary>Returns a pseudo-random double in the range [0.0, 1.0).</summary>
    public double NextDouble()
    {
        // 53 bits of randomness, matching double's mantissa precision.
        ulong bits = NextUInt64() >> 11;
        return bits * (1.0 / (1UL << 53));
    }

    private uint NextBoundedUInt32(uint exclusiveBound)
    {
        // Debiased bounded generation via rejection sampling (Lemire-style
        // threshold), avoiding modulo bias.
        uint threshold = unchecked((uint)(-(int)exclusiveBound)) % exclusiveBound;
        while (true)
        {
            uint value = NextUInt32();
            if (value >= threshold)
            {
                return value % exclusiveBound;
            }
        }
    }

    private ulong NextBoundedUInt64(ulong exclusiveBound)
    {
        // Debiased bounded generation via rejection sampling (Lemire-style
        // threshold), avoiding modulo bias. Same algorithm as the 32-bit version.
        ulong threshold = unchecked((ulong)(-(long)exclusiveBound)) % exclusiveBound;
        while (true)
        {
            ulong value = NextUInt64();
            if (value >= threshold)
            {
                return value % exclusiveBound;
            }
        }
    }

    private static uint RotateRight(uint value, int rotation)
    {
        return (value >> rotation) | (value << ((-rotation) & 31));
    }
}
