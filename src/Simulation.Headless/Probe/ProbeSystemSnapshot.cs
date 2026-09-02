namespace CapitalismSim.Simulation.Headless.Probe;

/// <summary>
/// The serializable state of a <see cref="ProbeSystem"/>, sufficient to
/// deterministically resume its random stream and accumulator.
/// </summary>
public sealed record ProbeSystemSnapshot
{
    /// <summary>The system identifier this snapshot corresponds to.</summary>
    public required string SystemId { get; init; }

    /// <summary>The current state of the probe's random stream.</summary>
    public required ulong RandomState { get; init; }

    /// <summary>The increment value for the random stream.</summary>
    public required ulong RandomIncrement { get; init; }

    /// <summary>The running accumulator folded from random draws so far.</summary>
    public required ulong Accumulator { get; init; }
}
