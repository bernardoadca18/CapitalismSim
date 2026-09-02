namespace CapitalismSim.Simulation.Core.Diagnostics;

/// <summary>
/// A minimal sink for diagnostic messages emitted by the simulation
/// kernel. <see cref="Simulation.Core"/> never writes to the console or any
/// other concrete output directly; hosts (such as
/// <c>Simulation.Headless</c>) provide an implementation appropriate for
/// their environment.
/// </summary>
public interface IDiagnosticSink
{
    void Write(string message);
}

/// <summary>
/// A sink that discards every message. Used as the default so the kernel
/// never pays formatting cost when diagnostics are not wired up, and hosts
/// that don't care about diagnostics don't need to provide a real sink.
/// </summary>
public sealed class NullDiagnosticSink : IDiagnosticSink
{
    public static readonly NullDiagnosticSink Instance = new();

    private NullDiagnosticSink()
    {
    }

    public void Write(string message)
    {
        // Intentionally discarded.
    }
}
