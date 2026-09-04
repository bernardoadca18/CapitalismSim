namespace CapitalismSim.Simulation.Core.Entities;

/// <summary>
/// A typed identifier for a monetary account in the simulation.
/// Backed by a global <see cref="EntityId"/> from the shared allocator.
/// </summary>
public readonly struct AccountId : IEquatable<AccountId>, IComparable<AccountId>
{
    public EntityId EntityId { get; }

    public AccountId(EntityId entityId)
    {
        EntityId = entityId;
    }

    public bool IsNone => EntityId.IsNone;

    public static AccountId None => new(EntityId.None);

    public static bool operator ==(AccountId left, AccountId right) => left.Equals(right);

    public static bool operator !=(AccountId left, AccountId right) => !left.Equals(right);

    public static bool operator <(AccountId left, AccountId right) => left.EntityId < right.EntityId;

    public static bool operator <=(AccountId left, AccountId right) => left.EntityId <= right.EntityId;

    public static bool operator >(AccountId left, AccountId right) => left.EntityId > right.EntityId;

    public static bool operator >=(AccountId left, AccountId right) => left.EntityId >= right.EntityId;

    public bool Equals(AccountId other) => EntityId.Equals(other.EntityId);

    public override bool Equals(object? obj) => obj is AccountId other && Equals(other);

    public override int GetHashCode() => EntityId.GetHashCode();

    public int CompareTo(AccountId other) => EntityId.CompareTo(other.EntityId);

    public override string ToString() => EntityId.IsNone ? "Account(none)" : $"Account(#{EntityId.Value})";
}
