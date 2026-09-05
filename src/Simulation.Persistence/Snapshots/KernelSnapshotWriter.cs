using CapitalismSim.Simulation.Core.Engine;

namespace CapitalismSim.Simulation.Persistence.Snapshots;

/// <summary>
/// Legacy writer that captures simulation state into <see cref="KernelSnapshotDto"/>.
/// </summary>
/// <remarks>
/// This type is maintained for backward compatibility. New code should prefer
/// <see cref="SimulationSnapshotWriter"/> and <see cref="SimulationSnapshotDto"/>.
/// This writer delegates to <see cref="SimulationSnapshotWriter"/> and converts the result.
/// </remarks>
public static class KernelSnapshotWriter
{
    /// <summary>
    /// The snapshot schema version produced by this build.
    /// </summary>
    /// <remarks>
    /// This is now derived from <see cref="SimulationSnapshotWriter.CurrentFormatVersion"/>.
    /// </remarks>
    public const int CurrentFormatVersion = SimulationSnapshotWriter.CurrentFormatVersion;

    /// <summary>
    /// Captures the current state of the simulation engine into a legacy kernel snapshot.
    /// </summary>
    public static KernelSnapshotDto Capture(SimulationEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);

        // Delegate to the new writer and convert
        var snapshot = SimulationSnapshotWriter.Capture(engine);
        return ConvertToLegacyFormat(snapshot);
    }

    private static KernelSnapshotDto ConvertToLegacyFormat(SimulationSnapshotDto snapshot)
    {
        return new KernelSnapshotDto
        {
            FormatVersion = snapshot.FormatVersion,
            WorldSeed = snapshot.Kernel.WorldSeed,
            CurrentInstantSeconds = snapshot.Kernel.CurrentInstantSeconds,
            NextEntityId = snapshot.Kernel.NextEntityId,
            NextEventSequenceNumber = snapshot.Kernel.NextEventSequenceNumber,
            Population = snapshot.Population,
            MoneyLedger = snapshot.MoneyLedger,
        };
    }
}
