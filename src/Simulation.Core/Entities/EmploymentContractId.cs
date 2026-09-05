namespace CapitalismSim.Simulation.Core.Entities;

/// <summary>
/// A typed identifier for an employment contract entity in the simulation.
/// Backed by a global <see cref="EntityId"/> from the shared allocator.
/// </summary>
public readonly struct EmploymentContractId : IEquatable<EmploymentContractId>, IComparable<EmploymentContractId>
{
    public EntityId EntityId { get; }

    public EmploymentContractId(EntityId entityId)
    {
        EntityId = entityId;
    }

    public bool IsNone => EntityId.IsNone;

    public static EmploymentContractId None => new(EntityId.None);

    public static bool operator ==(EmploymentContractId left, EmploymentContractId right) => left.Equals(right);

    public static bool operator !=(EmploymentContractId left, EmploymentContractId right) => !left.Equals(right);

    public static bool operator <(EmploymentContractId left, EmploymentContractId right) => left.EntityId < right.EntityId;

    public static bool operator <=(EmploymentContractId left, EmploymentContractId right) => left.EntityId <= right.EntityId;

    public static bool operator >(EmploymentContractId left, EmploymentContractId right) => left.EntityId > right.EntityId;

    public static bool operator >=(EmploymentContractId left, EmploymentContractId right) => left.EntityId >= right.EntityId;

    public bool Equals(EmploymentContractId other) => EntityId.Equals(other.EntityId);

    public override bool Equals(object? obj) => obj is EmploymentContractId other && Equals(other);

    public override int GetHashCode() => EntityId.GetHashCode();

    public int CompareTo(EmploymentContractId other) => EntityId.CompareTo(other.EntityId);

    public override string ToString() => EntityId.IsNone ? "EmploymentContract(none)" : $"EmploymentContract(#{EntityId.Value})";
}
