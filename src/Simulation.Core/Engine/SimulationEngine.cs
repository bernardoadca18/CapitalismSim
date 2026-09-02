using CapitalismSim.Simulation.Core.Commands;
using CapitalismSim.Simulation.Core.Diagnostics;
using CapitalismSim.Simulation.Core.Entities;
using CapitalismSim.Simulation.Core.Events;
using CapitalismSim.Simulation.Core.Randomness;
using CapitalismSim.Simulation.Core.Scheduling;
using CapitalismSim.Simulation.Core.Time;

namespace CapitalismSim.Simulation.Core.Engine;

/// <summary>
/// Coordinates the simulation kernel's runtime: the logical clock, the
/// scheduler, entity id allocation, randomness, commands, and events.
/// </summary>
/// <remarks>
/// <para>
/// The engine advances purely on request — there is no internal timer, no
/// background thread, and no dependency on wall-clock time. Callers decide
/// how much logical time to advance and when; this is what allows the
/// headless runner to simulate, for example, a century in a few seconds
/// when the host machine can keep up.
/// </para>
/// <para>
/// <see cref="SimulationEngine"/> is not thread-safe. It is designed to be
/// driven by a single logical thread of control, consistent with the
/// project's single-threaded-kernel decision (see
/// docs/adr/0003-deterministic-single-threaded-kernel.md). Parallelism, if
/// ever introduced, will be added deliberately and measured, not bolted
/// onto this type.
/// </para>
/// </remarks>
public sealed class SimulationEngine
{
    private bool _isAdvancing;

    public SimulationEngine(SimulationEngineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        WorldSeed = options.WorldSeed;
        Clock = new SimulationClock(options.StartInstant);
        Scheduler = new SimulationScheduler();
        EntityIds = new EntityIdAllocator();
        RandomStreams = new RandomStreamCatalog(options.WorldSeed);
        Commands = new CommandDispatcher();
        Events = new SimulationEventBus();
        Metrics = new SimulationMetrics();
        Diagnostics = options.DiagnosticSink ?? NullDiagnosticSink.Instance;
    }

    /// <summary>The root seed this world was created with.</summary>
    public ulong WorldSeed { get; }

    /// <summary>The kernel's logical clock.</summary>
    public SimulationClock Clock { get; }

    /// <summary>The deterministic system scheduler.</summary>
    public SimulationScheduler Scheduler { get; }

    /// <summary>The monotonic entity id allocator for this world.</summary>
    public EntityIdAllocator EntityIds { get; }

    /// <summary>The catalog used to derive independent random streams.</summary>
    public RandomStreamCatalog RandomStreams { get; }

    /// <summary>The in-process command dispatcher.</summary>
    public CommandDispatcher Commands { get; }

    /// <summary>The in-process event bus.</summary>
    public SimulationEventBus Events { get; }

    /// <summary>Plain runtime counters describing this engine's activity.</summary>
    public SimulationMetrics Metrics { get; }

    /// <summary>Where the kernel writes diagnostic text, if anywhere.</summary>
    public IDiagnosticSink Diagnostics { get; }

    /// <summary>
    /// Advances the simulation by a fixed duration, running every due
    /// system occurrence along the way.
    /// </summary>
    /// <returns>The number of individual system occurrences executed.</returns>
    public long AdvanceBy(SimulationDuration duration)
    {
        SimulationInstant target = Clock.Current.Add(duration);
        return AdvanceTo(target);
    }

    /// <summary>
    /// Advances the simulation up to and including <paramref name="targetInstant"/>,
    /// running every due system occurrence along the way.
    /// </summary>
    /// <returns>The number of individual system occurrences executed.</returns>
    /// <exception cref="ArgumentException"><paramref name="targetInstant"/> is before the current instant.</exception>
    /// <exception cref="InvalidOperationException">Called recursively from within a system tick.</exception>
    /// <remarks>
    /// <para>
    /// <strong>Exception behavior:</strong> If a system's tick throws, the exception propagates
    /// immediately. The reentrancy guard is released in a finally block, allowing subsequent
    /// advance calls. <see cref="Clock"/>.Current will be at the scheduled instant of the failed
    /// tick, not the target instant. The engine remains in a valid state for continued use.
    /// </para>
    /// <para>
    /// This is not transactional: completed ticks before an exception are not rolled back.
    /// </para>
    /// </remarks>
    public long AdvanceTo(SimulationInstant targetInstant)
    {
        if (_isAdvancing)
        {
            throw new InvalidOperationException(
                "Cannot call AdvanceTo recursively from within a system tick. " +
                "The simulation must complete its current advance before starting another.");
        }

        _isAdvancing = true;
        try
        {
            SimulationInstant previous = Clock.Current;

            long executed = Scheduler.RunDueOccurrences(previous, targetInstant, Clock);

            // After all occurrences have run, advance the clock to the target instant.
            // This ensures the clock is at the correct final state after the advance.
            Clock.AdvanceTo(targetInstant);

            Metrics.RecordAdvanceCall();
            Metrics.RecordSystemExecutions(executed);

            return executed;
        }
        finally
        {
            _isAdvancing = false;
        }
    }
}

/// <summary>Construction options for a new <see cref="SimulationEngine"/>.</summary>
public sealed class SimulationEngineOptions
{
    /// <summary>The root seed all determinism in this world derives from.</summary>
    public required ulong WorldSeed { get; init; }

    /// <summary>The instant the clock starts at. Defaults to <see cref="SimulationInstant.Epoch"/>.</summary>
    public SimulationInstant StartInstant { get; init; } = SimulationInstant.Epoch;

    /// <summary>Optional sink for kernel diagnostic messages.</summary>
    public IDiagnosticSink? DiagnosticSink { get; init; }
}
