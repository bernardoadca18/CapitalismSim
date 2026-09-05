using CapitalismSim.Simulation.Core.Entities;
using CapitalismSim.Simulation.Core.Economics;
using CapitalismSim.Simulation.Core.Employment;

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
/// - Compensation terms (pay and interval)
///
/// A position can be filled by at most one active contract at a time.
/// When filled, an EmploymentContract links the person to this position.
/// </remarks>
public readonly struct JobPosition : IEquatable<JobPosition>
{
    public JobPositionId Id { get; }
    public CompanyId CompanyId { get; }
    public JobRoleCode Role { get; }

    /// <summary>
    /// The compensation terms for this position.
    /// </summary>
    public CompensationTerms Compensation { get; }

    /// <summary>
    /// The pay amount per period in minor units (e.g., cents).
    /// </summary>
    public long PayAmountMinorUnits => Compensation.PayAmountMinorUnits;

    /// <summary>
    /// The pay interval in simulation days.
    /// </summary>
    public int PayIntervalDays => Compensation.PayIntervalDays;

    /// <summary>
    /// Creates a new job position.
    /// </summary>
    /// <param name="id">Unique position identifier.</param>
    /// <param name="companyId">The company that owns this position.</param>
    /// <param name="role">The job role for this position.</param>
    /// <param name="compensation">The compensation terms.</param>
    public JobPosition(
        JobPositionId id,
        CompanyId companyId,
        JobRoleCode role,
        CompensationTerms compensation)
    {
        if (compensation.IsNone)
        {
            throw new ArgumentException("Compensation terms cannot be None.", nameof(compensation));
        }

        // Note: CompensationTerms validates its own invariants

        Id = id;
        CompanyId = companyId;
        Role = role;
        Compensation = compensation;
    }

    /// <summary>
    /// Creates a new job position with explicit pay fields.
    /// Convenience overload for backward compatibility.
    /// </summary>
    public JobPosition(
        JobPositionId id,
        CompanyId companyId,
        JobRoleCode role,
        long payAmountMinorUnits,
        int payIntervalDays)
        : this(id, companyId, role, new CompensationTerms(payAmountMinorUnits, payIntervalDays))
    {
    }

    public bool IsNone => Id.IsNone;

    public static JobPosition None => new(JobPositionId.None, CompanyId.None, JobRoleCode.GeneralWorker, CompensationTerms.Zero(1));

    /// <summary>
    /// Gets the pay as a Money struct for the given currency.
    /// </summary>
    public Money GetPayAmount(CurrencyCode currency) => Compensation.GetPayAmount(currency);

    public bool Equals(JobPosition other) =>
        Id.Equals(other.Id) &&
        CompanyId.Equals(other.CompanyId) &&
        Role.Equals(other.Role) &&
        Compensation.Equals(other.Compensation);

    public override bool Equals(object? obj) => obj is JobPosition other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Id, CompanyId, Role, Compensation);

    public static bool operator ==(JobPosition left, JobPosition right) => left.Equals(right);

    public static bool operator !=(JobPosition left, JobPosition right) => !left.Equals(right);

    public override string ToString()
    {
        if (IsNone) return "JobPosition(none)";
        return $"JobPosition(#{Id.EntityId.Value}, company=#{CompanyId.EntityId.Value}, role={Role.Value})";
    }
}
