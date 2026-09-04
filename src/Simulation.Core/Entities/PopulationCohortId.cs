namespace CapitalismSim.Simulation.Core.Entities;

/// <summary>
/// A typed identifier for a population cohort in the simulation.
/// Backed by a global <see cref="EntityId"/> from the shared allocator.
/// </summary>
public readonly struct PopulationCohortId : IEquatable<PopulationCohortId>, IComparable<PopulationCohortId>
{
    public EntityId EntityId { get; }

    public PopulationCohortId(EntityId entityId)
    {
        EntityId = entityId;
    }

    public bool IsNone => EntityId.IsNone;

    public static PopulationCohortId None => new(EntityId.None);

    public static bool operator ==(PopulationCohortId left, PopulationCohortId right) => left.Equals(right);

    public static bool operator !=(PopulationCohortId left, PopulationCohortId right) => !left.Equals(right);

    public static bool operator <(PopulationCohortId left, PopulationCohortId right) => left.EntityId < right.EntityId;

    public static bool operator <=(PopulationCohortId left, PopulationCohortId right) => left.EntityId <= right.EntityId;

    public static bool operator >(PopulationCohortId left, PopulationCohortId right) => left.EntityId > right.EntityId;

    public static bool operator >=(PopulationCohortId left, PopulationCohortId right) => left.EntityId >= right.EntityId;

    public bool Equals(PopulationCohortId other) => EntityId.Equals(other.EntityId);

    public override bool Equals(object? obj) => obj is PopulationCohortId other && Equals(other);

    public override int GetHashCode() => EntityId.GetHashCode();

    public int CompareTo(PopulationCohortId other) => EntityId.CompareTo(other.EntityId);

    public override string ToString() => EntityId.IsNone ? "Cohort(none)" : $"Cohort(#{EntityId.Value})";
}
