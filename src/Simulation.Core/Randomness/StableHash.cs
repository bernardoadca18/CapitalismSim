using System.Text;

namespace CapitalismSim.Simulation.Core.Randomness;

/// <summary>
/// A stable, explicit 64-bit hash used to derive random stream seeds. This
/// is intentionally not <see cref="object.GetHashCode"/> or
/// <see cref="string.GetHashCode()"/>, whose algorithms are randomized
/// per-process by the runtime and are explicitly documented by Microsoft as
/// unsuitable for persistence — using either would break determinism across
/// runs and machines.
/// </summary>
/// <remarks>
/// The implementation is the 64-bit FNV-1a algorithm, a small, public,
/// non-cryptographic hash with a stable, fully specified definition.
/// </remarks>
public static class StableHash
{
    private const ulong FnvOffsetBasis = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    public static ulong ComputeUInt64(ReadOnlySpan<byte> data)
    {
        ulong hash = FnvOffsetBasis;
        foreach (byte b in data)
        {
            hash ^= b;
            hash = unchecked(hash * FnvPrime);
        }

        return hash;
    }

    public static ulong ComputeUInt64(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        int byteCount = Encoding.UTF8.GetByteCount(text);
        byte[] rented = System.Buffers.ArrayPool<byte>.Shared.Rent(byteCount);
        try
        {
            int written = Encoding.UTF8.GetBytes(text, rented);
            return ComputeUInt64(rented.AsSpan(0, written));
        }
        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(rented);
        }
    }

    /// <summary>
    /// Combines two already-computed hash values into one in an order-sensitive way.
    /// The result is a function of both (a, b) such that Combine(a, b) != Combine(b, a)
    /// in general, which is essential for fingerprinting ordered sequences.
    /// </summary>
    public static ulong Combine(ulong a, ulong b)
    {
        // Rotate a by a constant to ensure order-sensitivity: Combine(a,b) != Combine(b,a)
        // The rotation amount (17) is an arbitrary odd number < 64.
        ulong hash = (a << 17) | (a >> 47);
        hash ^= b;
        hash = unchecked(hash * FnvPrime);
        return hash;
    }
}
