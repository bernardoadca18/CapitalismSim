using CapitalismSim.Simulation.Core.Engine;

namespace CapitalismSim.Simulation.Persistence.Snapshots;

/// <summary>
/// Legacy reader that restores <see cref="KernelSnapshotDto"/> onto a simulation engine.
/// </summary>
/// <remarks>
/// This type is maintained for backward compatibility. New code should prefer
/// <see cref="SimulationSnapshotReader"/> and <see cref="SimulationSnapshotDto"/>.
/// This reader converts the legacy format and delegates to <see cref="SimulationSnapshotReader"/>.
/// </remarks>
public static class KernelSnapshotReader
{
    /// <summary>
    /// Validates the snapshot's format version and applies its state onto
    /// <paramref name="engine"/>.
    /// </summary>
    /// <exception cref="SnapshotFormatException">
    /// The snapshot's format version is not supported, its world seed
    /// does not match the target engine's world seed, or any snapshot field
    /// contains an invalid value.
    /// </exception>
    public static void Apply(SimulationEngine engine, KernelSnapshotDto snapshot)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(snapshot);

        // Convert legacy format to new format and delegate
        var newSnapshot = ConvertToNewFormat(snapshot);
        SimulationSnapshotReader.Apply(engine, newSnapshot);
    }

    private static SimulationSnapshotDto ConvertToNewFormat(KernelSnapshotDto legacy)
    {
        return new SimulationSnapshotDto
        {
            FormatVersion = legacy.FormatVersion,
            Kernel = new KernelStateDto
            {
                WorldSeed = legacy.WorldSeed,
                CurrentInstantSeconds = legacy.CurrentInstantSeconds,
                NextEntityId = legacy.NextEntityId,
                NextEventSequenceNumber = legacy.NextEventSequenceNumber,
            },
            Population = legacy.Population,
            MoneyLedger = legacy.MoneyLedger,
            Companies = null,
            Employment = null,
        };
    }
}
