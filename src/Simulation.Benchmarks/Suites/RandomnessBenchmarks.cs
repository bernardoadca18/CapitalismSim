using BenchmarkDotNet.Attributes;
using CapitalismSim.Simulation.Core.Randomness;

namespace CapitalismSim.Simulation.Benchmarks.Suites;

/// <summary>
/// Baseline throughput for the deterministic PRNG and for deriving
/// independent random streams. See docs/PERFORMANCE_BUDGET.md for how to
/// interpret results.
/// </summary>
[MemoryDiagnoser]
public class RandomnessBenchmarks
{
    private DeterministicRandom _random = null!;
    private RandomStreamCatalog _catalog = null!;

    [GlobalSetup]
    public void Setup()
    {
        _random = new DeterministicRandom(seed: 123456UL, streamSelector: 1UL);
        _catalog = new RandomStreamCatalog(rootSeed: 123456UL);
    }

    [Benchmark]
    public uint NextUInt32() => _random.NextUInt32();

    [Benchmark]
    public ulong NextUInt64() => _random.NextUInt64();

    [Benchmark]
    public double NextDouble() => _random.NextDouble();

    [Benchmark]
    public DeterministicRandom DeriveStream() => _catalog.GetStream(new RandomDomain("Benchmark"), purpose: "derive");
}
