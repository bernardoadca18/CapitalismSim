namespace CapitalismSim.Simulation.Core.Entities;

/// <summary>
/// A typed identifier for a company entity in the simulation.
/// Backed by a global <see cref="EntityId"/> from the shared allocator.
/// </summary>
public readonly struct CompanyId : IEquatable<CompanyId>, IComparable<CompanyId>
{
    public EntityId EntityId { get; }

    public CompanyId(EntityId entityId)
    {
        EntityId = entityId;
    }

    public bool IsNone => EntityId.IsNone;

    public static CompanyId None => new(EntityId.None);

    public static bool operator ==(CompanyId left, CompanyId right) => left.Equals(right);

    public static bool operator !=(CompanyId left, CompanyId right) => !left.Equals(right);

    public static bool operator <(CompanyId left, CompanyId right) => left.EntityId < right.EntityId;

    public static bool operator <=(CompanyId left, CompanyId right) => left.EntityId <= right.EntityId;

    public static bool operator >(CompanyId left, CompanyId right) => left.EntityId > right.EntityId;

    public static bool operator >=(CompanyId left, CompanyId right) => left.EntityId >= right.EntityId;

    public bool Equals(CompanyId other) => EntityId.Equals(other.EntityId);

    public override bool Equals(object? obj) => obj is CompanyId other && Equals(other);

    public override int GetHashCode() => EntityId.GetHashCode();

    public int CompareTo(CompanyId other) => EntityId.CompareTo(other.EntityId);

    public override string ToString() => EntityId.IsNone ? "Company(none)" : $"Company(#{EntityId.Value})";
}
