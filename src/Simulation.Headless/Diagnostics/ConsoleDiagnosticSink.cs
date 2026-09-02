using CapitalismSim.Simulation.Core.Diagnostics;

namespace CapitalismSim.Simulation.Headless.Diagnostics;

/// <summary>Writes kernel diagnostic messages to standard output.</summary>
public sealed class ConsoleDiagnosticSink : IDiagnosticSink
{
    public void Write(string message) => Console.WriteLine(message);
}
