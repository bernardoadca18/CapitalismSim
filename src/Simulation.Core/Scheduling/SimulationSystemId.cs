namespace CapitalismSim.Simulation.Core.Scheduling;

/// <summary>
/// A stable, human-readable identity for a scheduled system, used for
/// deterministic tie-breaking in the scheduler and for diagnostics/logging.
/// Two systems in the same <see cref="SimulationScheduler"/> must not share
/// an id.
/// </summary>
public readonly record struct SimulationSystemId
{
    public string Value { get; }

    public SimulationSystemId(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);
        Value = value;
    }

    public override string ToString() => Value;
}
