using BenchmarkDotNet.Attributes;
using CapitalismSim.Simulation.Core.Commands;
using CapitalismSim.Simulation.Core.Events;
using CapitalismSim.Simulation.Core.Time;

namespace CapitalismSim.Simulation.Benchmarks.Suites;

/// <summary>Baseline throughput for the event bus and command dispatcher.</summary>
[MemoryDiagnoser]
public class DispatchBenchmarks
{
    private SimulationEventBus _events = null!;
    private CommandDispatcher _commands = null!;

    [GlobalSetup]
    public void Setup()
    {
        _events = new SimulationEventBus();
        _events.Subscribe<BenchmarkEvent>(static _ => { });

        _commands = new CommandDispatcher();
        _commands.RegisterHandler<BenchmarkCommand>(new BenchmarkCommandHandler());
    }

    [Benchmark]
    public void PublishEvent() => _events.Publish(new BenchmarkEvent(1), SimulationInstant.Epoch);

    [Benchmark]
    public void DispatchCommand() => _commands.Dispatch(new BenchmarkCommand());

    private sealed record BenchmarkEvent(int Value) : ISimulationEvent;

    private sealed record BenchmarkCommand : ISimulationCommand;

    private sealed class BenchmarkCommandHandler : ISimulationCommandHandler<BenchmarkCommand>
    {
        public void Handle(BenchmarkCommand command)
        {
            // Intentionally empty.
        }
    }
}
