using BenchmarkDotNet.Attributes;
using CapitalismSim.Simulation.Core.Scheduling;
using CapitalismSim.Simulation.Core.Time;

namespace CapitalismSim.Simulation.Benchmarks.Suites;

/// <summary>
/// Baseline throughput for the scheduler with a small and a large number of
/// registered systems, advancing over a fixed simulated window each
/// iteration. This is the benchmark most representative of the kernel's
/// core hot path.
/// </summary>
[MemoryDiagnoser]
public class SchedulerBenchmarks
{
    private SimulationScheduler _fewSystems = null!;
    private SimulationScheduler _manySystems = null!;

    private static readonly SimulationDuration AdvanceWindow = SimulationDuration.FromDays(1);

    [GlobalSetup]
    public void Setup()
    {
        _fewSystems = BuildScheduler(systemCount: 5);
        _manySystems = BuildScheduler(systemCount: 200);
    }

    private static SimulationScheduler BuildScheduler(int systemCount)
    {
        var scheduler = new SimulationScheduler();
        for (int i = 0; i < systemCount; i++)
        {
            scheduler.Register(
                new NoOpSystem(new SimulationSystemId($"noop-{i}")),
                new SchedulingPolicy(SimulationInstant.Epoch, SimulationDuration.FromMinutes(1 + (i % 30))));
        }

        return scheduler;
    }

    [Benchmark]
    public long FewSystemsOneDay()
    {
        var clock = new SimulationClock(SimulationInstant.Epoch);
        return _fewSystems.RunDueOccurrences(SimulationInstant.Epoch, SimulationInstant.Epoch.Add(AdvanceWindow), clock);
    }

    [Benchmark]
    public long ManySystemsOneDay()
    {
        var clock = new SimulationClock(SimulationInstant.Epoch);
        return _manySystems.RunDueOccurrences(SimulationInstant.Epoch, SimulationInstant.Epoch.Add(AdvanceWindow), clock);
    }

    private sealed class NoOpSystem : ISimulationSystem
    {
        public NoOpSystem(SimulationSystemId id) => Id = id;

        public SimulationSystemId Id { get; }

        public void Tick(in SimulationTickContext context)
        {
            // Intentionally empty: isolates scheduler overhead from any
            // system body cost.
        }
    }
}
