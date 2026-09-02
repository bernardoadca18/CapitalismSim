using BenchmarkDotNet.Attributes;
using CapitalismSim.Simulation.Core.Entities;

namespace CapitalismSim.Simulation.Benchmarks.Suites;

/// <summary>Baseline throughput for entity id allocation.</summary>
[MemoryDiagnoser]
public class EntityIdBenchmarks
{
    private EntityIdAllocator _allocator = null!;

    [IterationSetup]
    public void Setup()
    {
        _allocator = new EntityIdAllocator();
    }

    [Benchmark]
    public EntityId Allocate() => _allocator.Allocate();
}
