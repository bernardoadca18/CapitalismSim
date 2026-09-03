using CapitalismSim.Simulation.Core.Engine;
using CapitalismSim.Simulation.Core.Time;

namespace CapitalismSim.Simulation.Persistence.Snapshots;

/// <summary>
/// Restores a previously captured <see cref="KernelSnapshotDto"/> onto a
/// freshly constructed <see cref="SimulationEngine"/>.
/// </summary>
/// <remarks>
/// The caller is responsible for constructing a new <see cref="SimulationEngine"/>
/// with the same <see cref="SimulationEngineOptions.WorldSeed"/> as the
/// snapshot before calling <see cref="Apply"/> (or simply reuse
/// <see cref="KernelSnapshotDto.WorldSeed"/> when constructing it). This
/// keeps engine construction explicit rather than having the persistence
/// layer reach into engine internals to change immutable configuration.
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

        if (snapshot.FormatVersion != KernelSnapshotWriter.CurrentFormatVersion)
        {
            throw new SnapshotFormatException(
                $"Cannot load snapshot with format version {snapshot.FormatVersion}; " +
                $"this build only supports format version {KernelSnapshotWriter.CurrentFormatVersion}.");
        }

        if (snapshot.WorldSeed != engine.WorldSeed)
        {
            throw new SnapshotFormatException(
                $"Snapshot world seed ({snapshot.WorldSeed}) does not match the target engine's " +
                $"world seed ({engine.WorldSeed}). Construct the engine with the snapshot's seed before restoring.");
        }

        if (snapshot.CurrentInstantSeconds < 0)
        {
            throw new SnapshotFormatException(
                $"Snapshot has an invalid CurrentInstantSeconds of {snapshot.CurrentInstantSeconds}; " +
                "time cannot be negative.");
        }

        if (snapshot.NextEntityId < 1)
        {
            throw new SnapshotFormatException(
                $"Snapshot has an invalid NextEntityId of {snapshot.NextEntityId}; it must be at least 1.");
        }

        if (snapshot.NextEventSequenceNumber < 1)
        {
            throw new SnapshotFormatException(
                $"Snapshot has an invalid NextEventSequenceNumber of {snapshot.NextEventSequenceNumber}; it must be at least 1.");
        }

        // Validate probe system snapshots if present
        if (snapshot.ProbeSystems is not null)
        {
            foreach (var kvp in snapshot.ProbeSystems)
            {
                if (string.IsNullOrWhiteSpace(kvp.Key))
                {
                    throw new SnapshotFormatException(
                        "Snapshot contains a probe system with a null or empty system id.");
                }

                ProbeSystemSnapshotDto probe = kvp.Value;
                if (probe is null)
                {
                    throw new SnapshotFormatException(
                        $"Snapshot contains a null probe system snapshot for id '{kvp.Key}'.");
                }

                // PCG32 requires an odd increment for valid operation
                if ((probe.RandomIncrement & 1UL) == 0UL)
                {
                    throw new SnapshotFormatException(
                        $"Snapshot contains a probe system '{kvp.Key}' with an even RandomIncrement " +
                        $"({probe.RandomIncrement}); PCG32 requires an odd increment.");
                }
            }
        }

        engine.Clock.Restore(new SimulationInstant(snapshot.CurrentInstantSeconds));
        engine.EntityIds.Restore(snapshot.NextEntityId);
        engine.Events.RestoreSequenceNumber(snapshot.NextEventSequenceNumber);
    }
}
