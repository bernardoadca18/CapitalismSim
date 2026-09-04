namespace CapitalismSim.Simulation.Core.Entities;

/// <summary>
/// A typed identifier for a household entity in the simulation.
/// Backed by a global <see cref="EntityId"/> from the shared allocator.
/// </summary>
public readonly struct HouseholdId : IEquatable<HouseholdId>, IComparable<HouseholdId>
{
    public EntityId EntityId { get; }

    public HouseholdId(EntityId entityId)
    {
        EntityId = entityId;
    }

    public bool IsNone => EntityId.IsNone;

    public static HouseholdId None => new(EntityId.None);

    public static bool operator ==(HouseholdId left, HouseholdId right) => left.Equals(right);

    public static bool operator !=(HouseholdId left, HouseholdId right) => !left.Equals(right);

    public static bool operator <(HouseholdId left, HouseholdId right) => left.EntityId < right.EntityId;

    public static bool operator <=(HouseholdId left, HouseholdId right) => left.EntityId <= right.EntityId;

    public static bool operator >(HouseholdId left, HouseholdId right) => left.EntityId > right.EntityId;

    public static bool operator >=(HouseholdId left, HouseholdId right) => left.EntityId >= right.EntityId;

    public bool Equals(HouseholdId other) => EntityId.Equals(other.EntityId);

    public override bool Equals(object? obj) => obj is HouseholdId other && Equals(other);

    public override int GetHashCode() => EntityId.GetHashCode();

    public int CompareTo(HouseholdId other) => EntityId.CompareTo(other.EntityId);

    public override string ToString() => EntityId.IsNone ? "Household(none)" : $"Household(#{EntityId.Value})";
}
