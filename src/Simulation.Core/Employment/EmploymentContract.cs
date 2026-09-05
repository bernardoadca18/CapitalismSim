using CapitalismSim.Simulation.Core.Entities;

namespace CapitalismSim.Simulation.Core.Employment;

/// <summary>
/// Represents an employment contract between a person and a company.
/// </summary>
/// <remarks>
/// An employment contract links a Person to a JobPosition at a Company.
/// It tracks:
/// - When the employment started (StartDayIndex)
/// - When it ended, if terminated (EndDayIndex, 0 if still active)
/// - Pay information (copied from the position at hire time)
///
/// The pay information is copied at hire time so that position changes
/// don't affect existing contracts. This allows companies to adjust
/// pay for new hires without retroactively changing existing employees.
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
    /// The simulation day the contract ended (0 if still active).
    /// </summary>
    public long EndDayIndex { get; }

    /// <summary>
    /// The pay amount per period in minor units (e.g., cents).
    /// Copied from the job position at hire time.
    /// </summary>
    public long PayAmountMinorUnits { get; }

    /// <summary>
    /// The pay interval in simulation days.
    /// Copied from the job position at hire time.
    /// </summary>
    public int PayIntervalDays { get; }

    /// <summary>
    /// Creates a new employment contract.
    /// </summary>
    public EmploymentContract(
        EmploymentContractId id,
        PersonId personId,
        CompanyId companyId,
        JobPositionId jobPositionId,
        long startDayIndex,
        long payAmountMinorUnits,
        int payIntervalDays)
    {
        if (startDayIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(startDayIndex), "Start day index cannot be negative.");
        }

        if (payAmountMinorUnits < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(payAmountMinorUnits), "Pay amount cannot be negative.");
        }

        if (payIntervalDays <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(payIntervalDays), "Pay interval must be positive.");
        }

        Id = id;
        PersonId = personId;
        CompanyId = companyId;
        JobPositionId = jobPositionId;
        StartDayIndex = startDayIndex;
        EndDayIndex = 0; // Active by default
        PayAmountMinorUnits = payAmountMinorUnits;
        PayIntervalDays = payIntervalDays;
    }

    public bool IsNone => Id.IsNone;

    public static EmploymentContract None => new(
        EmploymentContractId.None, PersonId.None, CompanyId.None, JobPositionId.None, 0, 0, 1);

    /// <summary>
    /// Whether this contract is currently active (not terminated).
    /// </summary>
    public bool IsActive => EndDayIndex == 0;

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
            Id, PersonId, CompanyId, JobPositionId, StartDayIndex, endDayIndex,
            PayAmountMinorUnits, PayIntervalDays);
    }

    /// <summary>
    /// Creates a new contract with a private constructor for WithEndDayIndex.
    /// </summary>
    private EmploymentContract(
        EmploymentContractId id,
        PersonId personId,
        CompanyId companyId,
        JobPositionId jobPositionId,
        long startDayIndex,
        long endDayIndex,
        long payAmountMinorUnits,
        int payIntervalDays)
    {
        Id = id;
        PersonId = personId;
        CompanyId = companyId;
        JobPositionId = jobPositionId;
        StartDayIndex = startDayIndex;
        EndDayIndex = endDayIndex;
        PayAmountMinorUnits = payAmountMinorUnits;
        PayIntervalDays = payIntervalDays;
    }

    public bool Equals(EmploymentContract other) =>
        Id.Equals(other.Id) &&
        PersonId.Equals(other.PersonId) &&
        CompanyId.Equals(other.CompanyId) &&
        JobPositionId.Equals(other.JobPositionId) &&
        StartDayIndex == other.StartDayIndex &&
        EndDayIndex == other.EndDayIndex &&
        PayAmountMinorUnits == other.PayAmountMinorUnits &&
        PayIntervalDays == other.PayIntervalDays;

    public override bool Equals(object? obj) => obj is EmploymentContract other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(
        Id, PersonId, CompanyId, JobPositionId, StartDayIndex, EndDayIndex, PayAmountMinorUnits, PayIntervalDays);

    public static bool operator ==(EmploymentContract left, EmploymentContract right) => left.Equals(right);

    public static bool operator !=(EmploymentContract left, EmploymentContract right) => !left.Equals(right);

    public override string ToString()
    {
        if (IsNone) return "EmploymentContract(none)";
        string status = IsActive ? "active" : $"ended-day-{EndDayIndex}";
        return $"EmploymentContract(#{Id.EntityId.Value}, person=#{PersonId.EntityId.Value}, company=#{CompanyId.EntityId.Value}, {status})";
    }
}
