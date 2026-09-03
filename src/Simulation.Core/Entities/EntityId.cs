namespace CapitalismSim.Simulation.Core.Entities;

/// <summary>
/// A lightweight, allocation-free identifier for a simulation entity,
/// backed by a 64-bit integer. Deliberately not a <see cref="Guid"/>:
/// future phases may need to allocate very large numbers of entities (or
/// aggregated population records), and a 16-byte GUID with no useful
/// ordering is unnecessarily expensive at that scale.
/// </summary>
/// <remarks>
/// This phase does not introduce domain-specific typed IDs such as
/// <c>PersonId</c> or <c>CompanyId</c> — those belong to the phases that
/// introduce those domains. <see cref="EntityId"/> is the single general
/// purpose identifier used until that need materializes.
/// </remarks>
public readonly struct EntityId : IEquatable<EntityId>, IComparable<EntityId>
{
    /// <summary>
    /// The reserved value representing "no entity". Never allocated by
    /// <see cref="EntityIdAllocator"/>.
    /// </summary>
    public static readonly EntityId None = new(0);

    public long Value { get; }

    public EntityId(long value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "EntityId must be non-negative. Use EntityId.None for 'no entity'.");
        }
        Value = value;
    }

    public bool IsNone => Value == 0;

    public static bool operator ==(EntityId left, EntityId right) => left.Equals(right);

    public static bool operator !=(EntityId left, EntityId right) => !left.Equals(right);

    public static bool operator <(EntityId left, EntityId right) => left.Value < right.Value;

    public static bool operator <=(EntityId left, EntityId right) => left.Value <= right.Value;

    public static bool operator >(EntityId left, EntityId right) => left.Value > right.Value;

    public static bool operator >=(EntityId left, EntityId right) => left.Value >= right.Value;

    public bool Equals(EntityId other) => Value == other.Value;

    public override bool Equals(object? obj) => obj is EntityId other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode();

    public int CompareTo(EntityId other) => Value.CompareTo(other.Value);

    public override string ToString() => IsNone ? "Entity(none)" : $"Entity(#{Value})";
}
