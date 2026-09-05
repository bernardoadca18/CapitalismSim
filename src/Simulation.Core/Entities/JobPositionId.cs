namespace CapitalismSim.Simulation.Core.Entities;

/// <summary>
/// A typed identifier for a job position entity in the simulation.
/// Backed by a global <see cref="EntityId"/> from the shared allocator.
/// </summary>
public readonly struct JobPositionId : IEquatable<JobPositionId>, IComparable<JobPositionId>
{
    public EntityId EntityId { get; }

    public JobPositionId(EntityId entityId)
    {
        EntityId = entityId;
    }

    public bool IsNone => EntityId.IsNone;

    public static JobPositionId None => new(EntityId.None);

    public static bool operator ==(JobPositionId left, JobPositionId right) => left.Equals(right);

    public static bool operator !=(JobPositionId left, JobPositionId right) => !left.Equals(right);

    public static bool operator <(JobPositionId left, JobPositionId right) => left.EntityId < right.EntityId;

    public static bool operator <=(JobPositionId left, JobPositionId right) => left.EntityId <= right.EntityId;

    public static bool operator >(JobPositionId left, JobPositionId right) => left.EntityId > right.EntityId;

    public static bool operator >=(JobPositionId left, JobPositionId right) => left.EntityId >= right.EntityId;

    public bool Equals(JobPositionId other) => EntityId.Equals(other.EntityId);

    public override bool Equals(object? obj) => obj is JobPositionId other && Equals(other);

    public override int GetHashCode() => EntityId.GetHashCode();

    public int CompareTo(JobPositionId other) => EntityId.CompareTo(other.EntityId);

    public override string ToString() => EntityId.IsNone ? "JobPosition(none)" : $"JobPosition(#{EntityId.Value})";
}
