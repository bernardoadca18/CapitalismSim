namespace CapitalismSim.Simulation.Persistence.Snapshots;

/// <summary>
/// Thrown when a snapshot cannot be loaded because its format version is
/// unrecognized, or its content is structurally invalid. Loading code must
/// never silently guess at an unfamiliar format; it must fail loudly and
/// specifically, per the project's persistence-versioning requirement.
/// </summary>
public sealed class SnapshotFormatException : Exception
{
    public SnapshotFormatException(string message) : base(message)
    {
    }

    public SnapshotFormatException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
