using CapitalismSim.Simulation.Core.Entities;
using CapitalismSim.Simulation.Core.Economics;

namespace CapitalismSim.Simulation.Core.Companies;

/// <summary>
/// Represents a job role code (e.g., "general-worker", "manager", "specialist").
/// </summary>
/// <remarks>
/// Job roles are string identifiers that categorize positions.
/// Future phases may add skill requirements, salary bands, etc.
/// </remarks>
public readonly record struct JobRoleCode
{
    public string Value { get; }

    private JobRoleCode(string value)
    {
        Value = value;
    }

    public static JobRoleCode GeneralWorker => new("general-worker");
    public static JobRoleCode Manager => new("manager");
    public static JobRoleCode Specialist => new("specialist");

    public static JobRoleCode FromString(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Job role code cannot be null or whitespace.", nameof(value));
        }
        return new JobRoleCode(value);
    }

    public override string ToString() => Value;
}

/// <summary>
/// Represents a job position at a company that can be filled by an employee.
/// </summary>
/// <remarks>
/// A job position represents a role that a company is hiring for.
/// Each position has:
/// - A unique JobPositionId
/// - A CompanyId for the owning company
/// - A JobRoleCode categorizing the role
/// - Pay information (amount and interval)
///
/// A position can be filled by at most one employee at a time.
/// When filled, an EmploymentContract links the person to this position.
/// </remarks>
public readonly struct JobPosition : IEquatable<JobPosition>
{
    public JobPositionId Id { get; }
    public CompanyId CompanyId { get; }
    public JobRoleCode Role { get; }

    /// <summary>
    /// The pay amount per period in minor units (e.g., cents).
    /// </summary>
    public long PayAmountMinorUnits { get; }

    /// <summary>
    /// The pay interval in simulation days.
    /// </summary>
    public int PayIntervalDays { get; }

    /// <summary>
    /// Creates a new job position.
    /// </summary>
    /// <param name="id">Unique position identifier.</param>
    /// <param name="companyId">The company that owns this position.</param>
    /// <param name="role">The job role for this position.</param>
    /// <param name="payAmountMinorUnits">Pay amount per period in minor units.</param>
    /// <param name="payIntervalDays">Pay interval in days (e.g., 7 for weekly, 14 for bi-weekly, 30 for monthly).</param>
    public JobPosition(
        JobPositionId id,
        CompanyId companyId,
        JobRoleCode role,
        long payAmountMinorUnits,
        int payIntervalDays)
    {
        if (payAmountMinorUnits < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(payAmountMinorUnits), "Pay amount cannot be negative.");
        }

        if (payIntervalDays <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(payIntervalDays), "Pay interval must be positive.");
        }

        Id = id;
        CompanyId = companyId;
        Role = role;
        PayAmountMinorUnits = payAmountMinorUnits;
        PayIntervalDays = payIntervalDays;
    }

    public bool IsNone => Id.IsNone;

    public static JobPosition None => new(JobPositionId.None, CompanyId.None, JobRoleCode.GeneralWorker, 0, 1);

    /// <summary>
    /// Gets the pay as a Money struct for the given currency.
    /// </summary>
    public Money GetPayAmount(CurrencyCode currency) => new(PayAmountMinorUnits, currency);

    public bool Equals(JobPosition other) =>
        Id.Equals(other.Id) &&
        CompanyId.Equals(other.CompanyId) &&
        Role.Equals(other.Role) &&
        PayAmountMinorUnits == other.PayAmountMinorUnits &&
        PayIntervalDays == other.PayIntervalDays;

    public override bool Equals(object? obj) => obj is JobPosition other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Id, CompanyId, Role, PayAmountMinorUnits, PayIntervalDays);

    public static bool operator ==(JobPosition left, JobPosition right) => left.Equals(right);

    public static bool operator !=(JobPosition left, JobPosition right) => !left.Equals(right);

    public override string ToString()
    {
        if (IsNone) return "JobPosition(none)";
        return $"JobPosition(#{Id.EntityId.Value}, company=#{CompanyId.EntityId.Value}, role={Role.Value})";
    }
}
