using CapitalismSim.Simulation.Core.Entities;

namespace CapitalismSim.Simulation.Core.Companies;

/// <summary>
/// Represents a company in the simulation.
/// </summary>
/// <remarks>
/// A company is an economic entity that can hire workers, produce goods/services,
/// and manage a treasury account. Companies are created with a name and a treasury
/// account for managing their finances.
///
/// Phase 3 design:
/// - Each company has a unique CompanyId and a treasury account
/// - Companies can own job positions which they can hire workers into
/// - The treasury account must exist in the MoneyLedger
/// </remarks>
public sealed class Company
{
    public CompanyId Id { get; }
    public string Name { get; }
    public AccountId TreasuryAccountId { get; }

    /// <summary>
    /// The job positions owned by this company.
    /// </summary>
    private readonly HashSet<JobPositionId> _positions = new();

    /// <summary>
    /// Gets the job positions owned by this company.
    /// </summary>
    public IReadOnlySet<JobPositionId> Positions => _positions;

    /// <summary>
    /// Creates a new company.
    /// </summary>
    /// <param name="id">Unique company identifier.</param>
    /// <param name="name">Company name.</param>
    /// <param name="treasuryAccountId">The company's treasury account for managing finances.</param>
    public Company(CompanyId id, string name, AccountId treasuryAccountId)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Company name cannot be null or whitespace.", nameof(name));
        }

        Id = id;
        Name = name;
        TreasuryAccountId = treasuryAccountId;
    }

    /// <summary>
    /// Adds a job position to this company.
    /// </summary>
    internal void AddPosition(JobPositionId positionId)
    {
        _positions.Add(positionId);
    }

    /// <summary>
    /// Removes a job position from this company.
    /// </summary>
    internal void RemovePosition(JobPositionId positionId)
    {
        _positions.Remove(positionId);
    }

    /// <summary>
    /// Checks if this company owns a specific job position.
    /// </summary>
    public bool HasPosition(JobPositionId positionId) => _positions.Contains(positionId);
}
