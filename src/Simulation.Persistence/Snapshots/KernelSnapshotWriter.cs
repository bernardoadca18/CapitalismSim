using CapitalismSim.Simulation.Core.Engine;

namespace CapitalismSim.Simulation.Persistence.Snapshots;

/// <summary>
/// Captures the current state of a <see cref="SimulationEngine"/> into a
/// <see cref="KernelSnapshotDto"/>.
/// </summary>
public static class KernelSnapshotWriter
{
    /// <summary>
    /// The snapshot schema version produced by this build. Bump this and
    /// add explicit migration/rejection logic whenever the schema changes
    /// incompatibly.
    /// </summary>
    public const int CurrentFormatVersion = 2;

    public static KernelSnapshotDto Capture(SimulationEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);

        return new KernelSnapshotDto
        {
            FormatVersion = CurrentFormatVersion,
            WorldSeed = engine.WorldSeed,
            CurrentInstantSeconds = engine.Clock.Current.TotalSeconds,
            NextEntityId = engine.EntityIds.NextValue,
            NextEventSequenceNumber = engine.Events.NextSequenceNumber,
            ProbeSystems = new Dictionary<string, ProbeSystemSnapshotDto>(),
        };
    }
}
