using BenchmarkDotNet.Attributes;
using CapitalismSim.Simulation.Core.Engine;
using CapitalismSim.Simulation.Core.Scheduling;
using CapitalismSim.Simulation.Core.Time;

namespace CapitalismSim.Simulation.Benchmarks.Suites;

/// <summary>
/// End-to-end baseline: advancing a fully wired engine (clock + scheduler
/// with a handful of systems) by a fixed simulated window. This is the
/// closest single number to "how fast can we simulate time" for this
/// phase's kernel.
/// </summary>
[MemoryDiagnoser]
public class SimulationAdvanceBenchmarks
{
    private SimulationEngine _engine = null!;

    [IterationSetup]
    public void Setup()
    {
        _engine = new SimulationEngine(new SimulationEngineOptions { WorldSeed = 7UL });
        for (int i = 0; i < 10; i++)
        {
            _engine.Scheduler.Register(
                new NoOpSystem(new SimulationSystemId($"system-{i}")),
                new SchedulingPolicy(SimulationInstant.Epoch, SimulationDuration.FromMinutes(5 + i)));
        }
    }

    [Benchmark]
    public long AdvanceOneSimulatedYear() => _engine.AdvanceBy(SimulationDuration.FromDays(365));

    private sealed class NoOpSystem : ISimulationSystem
    {
        public NoOpSystem(SimulationSystemId id) => Id = id;

        public SimulationSystemId Id { get; }

        public void Tick(in SimulationTickContext context)
        {
            // Intentionally empty.
        }
    }
}
