using BenchmarkDotNet.Attributes;
using CapitalismSim.Simulation.Core.Engine;
using CapitalismSim.Simulation.Persistence.Snapshots;

namespace CapitalismSim.Simulation.Benchmarks.Suites;

/// <summary>Baseline throughput for kernel snapshot capture and JSON round-trip.</summary>
[MemoryDiagnoser]
public class SnapshotBenchmarks
{
    private SimulationEngine _engine = null!;
    private KernelSnapshotDto _snapshot = null!;
    private string _serialized = null!;

    [GlobalSetup]
    public void Setup()
    {
        _engine = new SimulationEngine(new SimulationEngineOptions { WorldSeed = 42UL });
        // Allocate some entities to make the snapshot more realistic
        for (int i = 0; i < 50; i++)
        {
            _engine.EntityIds.Allocate();
        }

        _snapshot = KernelSnapshotWriter.Capture(_engine);
        _serialized = System.Text.Json.JsonSerializer.Serialize(_snapshot);
    }

    [Benchmark]
    public KernelSnapshotDto Capture() => KernelSnapshotWriter.Capture(_engine);

    [Benchmark]
    public string Serialize() => System.Text.Json.JsonSerializer.Serialize(_snapshot);

    [Benchmark]
    public KernelSnapshotDto? Deserialize() => System.Text.Json.JsonSerializer.Deserialize<KernelSnapshotDto>(_serialized);
}
