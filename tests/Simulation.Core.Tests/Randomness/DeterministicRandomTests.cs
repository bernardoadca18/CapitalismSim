using CapitalismSim.Simulation.Core.Randomness;
using Xunit;

namespace CapitalismSim.Simulation.Core.Tests.Randomness;

public sealed class DeterministicRandomTests
{
    [Fact]
    public void SameSeedAndStream_ProducesSameSequence()
    {
        var a = new DeterministicRandom(seed: 42UL, streamSelector: 54UL);
        var b = new DeterministicRandom(seed: 42UL, streamSelector: 54UL);

        for (int i = 0; i < 20; i++)
        {
            Assert.Equal(a.NextUInt32(), b.NextUInt32());
        }
    }

    [Fact]
    public void DifferentStreamSelector_ProducesDifferentSequence()
    {
        var a = new DeterministicRandom(seed: 42UL, streamSelector: 54UL);
        var b = new DeterministicRandom(seed: 42UL, streamSelector: 55UL);

        uint[] fromA = new uint[10];
        uint[] fromB = new uint[10];
        for (int i = 0; i < 10; i++)
        {
            fromA[i] = a.NextUInt32();
            fromB[i] = b.NextUInt32();
        }

        Assert.NotEqual(fromA, fromB);
    }

    [Fact]
    public void DifferentSeed_ProducesDifferentSequence()
    {
        var a = new DeterministicRandom(seed: 1UL, streamSelector: 1UL);
        var b = new DeterministicRandom(seed: 2UL, streamSelector: 1UL);

        Assert.NotEqual(a.NextUInt32(), b.NextUInt32());
    }

    [Fact]
    public void KnownVector_Seed42Stream54_MatchesRecordedSequence()
    {
        // These values were captured once from this exact implementation
        // and must remain stable forever: if this test ever needs to
        // change, the PRNG's *sequence* has changed, which is a breaking
        // change for every save file that depends on it.
        var rng = new DeterministicRandom(seed: 42UL, streamSelector: 54UL);

        uint[] expected =
        {
            2707161783u,
            2068313097u,
            3122475824u,
            2211639955u,
            3215226955u,
        };

        foreach (uint expectedValue in expected)
        {
            Assert.Equal(expectedValue, rng.NextUInt32());
        }
    }

    [Fact]
    public void FromState_ResumesExactlyWhereCaptured()
    {
        var original = new DeterministicRandom(seed: 42UL, streamSelector: 54UL);
        original.NextUInt32();
        original.NextUInt32();

        ulong capturedState = original.State;
        ulong capturedIncrement = original.Increment;

        uint expectedNext = original.NextUInt32();

        var resumed = DeterministicRandom.FromState(capturedState, capturedIncrement);
        uint actualNext = resumed.NextUInt32();

        Assert.Equal(expectedNext, actualNext);
    }

    [Fact]
    public void NextInt32_StaysWithinBounds()
    {
        var rng = new DeterministicRandom(seed: 7UL, streamSelector: 3UL);

        for (int i = 0; i < 1000; i++)
        {
            int value = rng.NextInt32(10, 20);
            Assert.InRange(value, 10, 19);
        }
    }

    [Fact]
    public void NextInt32_InvalidRange_Throws()
    {
        var rng = new DeterministicRandom(seed: 1UL, streamSelector: 1UL);
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt32(10, 10));
    }

    [Fact]
    public void NextDouble_StaysWithinUnitRange()
    {
        var rng = new DeterministicRandom(seed: 9UL, streamSelector: 9UL);

        for (int i = 0; i < 1000; i++)
        {
            double value = rng.NextDouble();
            Assert.InRange(value, 0.0, 1.0);
            Assert.True(value < 1.0);
        }
    }
}
