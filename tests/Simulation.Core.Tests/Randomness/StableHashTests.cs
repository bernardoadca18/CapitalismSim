using CapitalismSim.Simulation.Core.Randomness;
using Xunit;

namespace CapitalismSim.Simulation.Core.Tests.Randomness;

public sealed class StableHashTests
{
    [Fact]
    public void SameInput_AlwaysProducesSameHash()
    {
        ulong first = StableHash.ComputeUInt64("hello world");
        ulong second = StableHash.ComputeUInt64("hello world");

        Assert.Equal(first, second);
    }

    [Fact]
    public void DifferentInputs_ProduceDifferentHashes()
    {
        ulong a = StableHash.ComputeUInt64("alpha");
        ulong b = StableHash.ComputeUInt64("beta");

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void KnownVector_HelloString_MatchesRecordedHash()
    {
        // Captured once from this exact implementation; must remain stable.
        Assert.Equal(11831194018420276491UL, StableHash.ComputeUInt64("hello"));
    }

    [Fact]
    public void Combine_IsOrderSensitive()
    {
        ulong ab = StableHash.Combine(1UL, 2UL);
        ulong ba = StableHash.Combine(2UL, 1UL);

        Assert.NotEqual(ab, ba);
    }

    [Fact]
    public void Combine_IsDeterministic()
    {
        Assert.Equal(StableHash.Combine(10UL, 20UL), StableHash.Combine(10UL, 20UL));
    }
}
