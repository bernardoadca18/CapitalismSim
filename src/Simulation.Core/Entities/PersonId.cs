namespace CapitalismSim.Simulation.Core.Entities;

/// <summary>
/// A typed identifier for a person entity in the simulation.
/// Backed by a global <see cref="EntityId"/> from the shared allocator.
/// </summary>
public readonly struct PersonId : IEquatable<PersonId>, IComparable<PersonId>
{
    public EntityId EntityId { get; }

    public PersonId(EntityId entityId)
    {
        EntityId = entityId;
    }

    public bool IsNone => EntityId.IsNone;

    public static PersonId None => new(EntityId.None);

    public static bool operator ==(PersonId left, PersonId right) => left.Equals(right);

    public static bool operator !=(PersonId left, PersonId right) => !left.Equals(right);

    public static bool operator <(PersonId left, PersonId right) => left.EntityId < right.EntityId;

    public static bool operator <=(PersonId left, PersonId right) => left.EntityId <= right.EntityId;

    public static bool operator >(PersonId left, PersonId right) => left.EntityId > right.EntityId;

    public static bool operator >=(PersonId left, PersonId right) => left.EntityId >= right.EntityId;

    public bool Equals(PersonId other) => EntityId.Equals(other.EntityId);

    public override bool Equals(object? obj) => obj is PersonId other && Equals(other);

    public override int GetHashCode() => EntityId.GetHashCode();

    public int CompareTo(PersonId other) => EntityId.CompareTo(other.EntityId);

    public override string ToString() => EntityId.IsNone ? "Person(none)" : $"Person(#{EntityId.Value})";
}
