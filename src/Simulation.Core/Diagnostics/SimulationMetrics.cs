namespace CapitalismSim.Simulation.Core.Diagnostics;

/// <summary>
/// A small set of plain counters describing kernel activity, sufficient for
/// the headless runner and tests to report on what happened during a run.
/// This is not an observability framework (no OpenTelemetry, no metrics
/// server) — just in-memory counters read at the end of a run.
/// </summary>
/// <remarks>
/// Event and command counts are not tracked here because the
/// <see cref="Events.SimulationEventBus"/> and <see cref="Commands.CommandDispatcher"/>
/// are the authoritative sources for those metrics. Use
/// <c>engine.Events.TotalPublishedCount</c> and <c>engine.Commands.TotalDispatchCount</c>
/// instead.
/// </remarks>
public sealed class SimulationMetrics
{
    /// <summary>Number of times <see cref="Engine.SimulationEngine.AdvanceBy"/> or
    /// <see cref="Engine.SimulationEngine.AdvanceTo"/> was called.</summary>
    public long AdvanceCallCount { get; private set; }

    /// <summary>Total number of individual system tick occurrences executed.</summary>
    public long SystemExecutionCount { get; private set; }

    public void RecordAdvanceCall() => AdvanceCallCount++;

    public void RecordSystemExecutions(long count) => SystemExecutionCount += count;
}
