using CapitalismSim.Simulation.Core.Entities;

namespace CapitalismSim.Simulation.Core.Employment;

/// <summary>
/// Represents an employment contract between a person and a company.
/// </summary>
/// <remarks>
/// An employment contract links a Person to a JobPosition at a Company.
/// It tracks:
/// - When the employment started (StartDayIndex)
/// - When it ended, if terminated (EndDayIndex, null if still active)
/// - Compensation terms (copied from the position at hire time)
///
/// The compensation is copied at hire time so that position changes
/// don't affect existing contracts. This allows companies to adjust
/// pay for new hires without retroactively changing existing employees.
///
/// EndDayIndex semantics:
/// - null = contract is active
/// - value = contract terminated on that day
/// This avoids the ambiguity of "0 could mean active or terminated on day 0".
/// </remarks>
public readonly struct EmploymentContract : IEquatable<EmploymentContract>
{
    public EmploymentContractId Id { get; }
    public PersonId PersonId { get; }
    public CompanyId CompanyId { get; }
    public JobPositionId JobPositionId { get; }

    /// <summary>
    /// The simulation day the contract started (days since epoch).
    /// </summary>
    public long StartDayIndex { get; }

    /// <summary>
    /// The simulation day the contract ended, or null if still active.
    /// </summary>
    public long? EndDayIndex { get; }

    /// <summary>
    /// The compensation terms (pay and interval).
    /// Copied from the job position at hire time.
    /// </summary>
    public CompensationTerms Compensation { get; }

    /// <summary>
    /// The pay amount per period in minor units (e.g., cents).
    /// Copied from the job position at hire time.
    /// </summary>
    public long PayAmountMinorUnits => Compensation.PayAmountMinorUnits;

    /// <summary>
    /// The pay interval in simulation days.
    /// Copied from the job position at hire time.
    /// </summary>
    public int PayIntervalDays => Compensation.PayIntervalDays;

    /// <summary>
    /// Creates a new employment contract.
    /// </summary>
    public EmploymentContract(
        EmploymentContractId id,
        PersonId personId,
        CompanyId companyId,
        JobPositionId jobPositionId,
        long startDayIndex,
        CompensationTerms compensation)
    {
        if (startDayIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(startDayIndex), "Start day index cannot be negative.");
        }

        if (compensation.IsNone)
        {
            throw new ArgumentException("Compensation terms cannot be None.", nameof(compensation));
        }

        Id = id;
        PersonId = personId;
        CompanyId = companyId;
        JobPositionId = jobPositionId;
        StartDayIndex = startDayIndex;
        EndDayIndex = null; // Active by default
        Compensation = compensation;
    }

    /// <summary>
    /// Creates a new employment contract with explicit pay fields.
    /// Convenience overload for backward compatibility.
    /// </summary>
    public EmploymentContract(
        EmploymentContractId id,
        PersonId personId,
        CompanyId companyId,
        JobPositionId jobPositionId,
        long startDayIndex,
        long payAmountMinorUnits,
        int payIntervalDays)
        : this(id, personId, companyId, jobPositionId, startDayIndex,
            new CompensationTerms(payAmountMinorUnits, payIntervalDays))
    {
    }

    public bool IsNone => Id.IsNone;

    public static EmploymentContract None => new(
        EmploymentContractId.None, PersonId.None, CompanyId.None, JobPositionId.None, 0,
        CompensationTerms.None);

    /// <summary>
    /// Whether this contract is currently active (not terminated).
    /// </summary>
    public bool IsActive => EndDayIndex is null;

    /// <summary>
    /// Returns a new contract with the specified end day.
    /// </summary>
    public EmploymentContract WithEndDayIndex(long endDayIndex)
    {
        if (endDayIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(endDayIndex), "End day index cannot be negative.");
        }

        return new EmploymentContract(
            Id, PersonId, CompanyId, JobPositionId, StartDayIndex, endDayIndex, Compensation);
    }

    /// <summary>
    /// Private constructor for creating terminated contracts.
    /// </summary>
    private EmploymentContract(
        EmploymentContractId id,
        PersonId personId,
        CompanyId companyId,
        JobPositionId jobPositionId,
        long startDayIndex,
        long? endDayIndex,
        CompensationTerms compensation)
    {
        Id = id;
        PersonId = personId;
        CompanyId = companyId;
        JobPositionId = jobPositionId;
        StartDayIndex = startDayIndex;
        EndDayIndex = endDayIndex;
        Compensation = compensation;
    }

    public bool Equals(EmploymentContract other) =>
        Id.Equals(other.Id) &&
        PersonId.Equals(other.PersonId) &&
        CompanyId.Equals(other.CompanyId) &&
        JobPositionId.Equals(other.JobPositionId) &&
        StartDayIndex == other.StartDayIndex &&
        EndDayIndex == other.EndDayIndex &&
        Compensation.Equals(other.Compensation);

    public override bool Equals(object? obj) => obj is EmploymentContract other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(
        Id, PersonId, CompanyId, JobPositionId, StartDayIndex, EndDayIndex, Compensation);

    public static bool operator ==(EmploymentContract left, EmploymentContract right) => left.Equals(right);

    public static bool operator !=(EmploymentContract left, EmploymentContract right) => !left.Equals(right);

    public override string ToString()
    {
        if (IsNone) return "EmploymentContract(none)";
        string status = IsActive ? "active" : $"ended-day-{EndDayIndex}";
        return $"EmploymentContract(#{Id.EntityId.Value}, person=#{PersonId.EntityId.Value}, company=#{CompanyId.EntityId.Value}, {status})";
    }
}
