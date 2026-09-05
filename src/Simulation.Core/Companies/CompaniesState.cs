using CapitalismSim.Simulation.Core.Entities;
using CapitalismSim.Simulation.Core.Economics;

namespace CapitalismSim.Simulation.Core.Companies;

/// <summary>
/// Manages all companies and job positions in the simulation.
/// </summary>
/// <remarks>
/// Design invariants:
/// - Each company has a unique CompanyId
/// - Each company has a treasury account that must exist in the MoneyLedger
/// - Each job position belongs to exactly one company
/// - Job positions are unique within a company
///
/// All operations are atomic: either fully succeed or leave state unchanged.
/// </remarks>
public sealed class CompaniesState
{
    private readonly Dictionary<CompanyId, Company> _companies = new();
    private readonly Dictionary<JobPositionId, JobPosition> _positions = new();

    /// <summary>Number of companies.</summary>
    public int CompanyCount => _companies.Count;

    /// <summary>Number of job positions.</summary>
    public int PositionCount => _positions.Count;

    /// <summary>Gets all company IDs.</summary>
    public IEnumerable<CompanyId> GetAllCompanyIds() => _companies.Keys;

    /// <summary>Gets all job position IDs.</summary>
    public IEnumerable<JobPositionId> GetAllPositionIds() => _positions.Keys;

    /// <summary>
    /// Creates a new company with the given treasury account.
    /// </summary>
    /// <param name="company">The company to create.</param>
    /// <param name="ledger">The money ledger to validate the treasury account.</param>
    /// <exception cref="InvalidOperationException">Company already exists or treasury account is invalid.</exception>
    public void CreateCompany(Company company, MoneyLedger ledger)
    {
        ArgumentNullException.ThrowIfNull(company);
        ArgumentNullException.ThrowIfNull(ledger);

        if (_companies.ContainsKey(company.Id))
        {
            throw new InvalidOperationException($"Company {company.Id} already exists.");
        }

        // Validate treasury account exists
        if (!ledger.AccountExists(company.TreasuryAccountId))
        {
            throw new InvalidOperationException(
                $"Cannot create company {company.Id}: treasury account {company.TreasuryAccountId} does not exist.");
        }

        // Validate treasury account owner matches company
        var owner = ledger.GetOwner(company.TreasuryAccountId);
        if (owner != company.Id.EntityId)
        {
            throw new InvalidOperationException(
                $"Cannot create company {company.Id}: treasury account {company.TreasuryAccountId} is owned by {owner}, expected {company.Id.EntityId}.");
        }

        _companies[company.Id] = company;
    }

    /// <summary>
    /// Gets a company by ID. Returns null if not found.
    /// </summary>
    public Company? GetCompany(CompanyId companyId)
    {
        return _companies.TryGetValue(companyId, out var company) ? company : null;
    }

    /// <summary>
    /// Checks if a company exists.
    /// </summary>
    public bool CompanyExists(CompanyId companyId) => _companies.ContainsKey(companyId);

    /// <summary>
    /// Adds a job position to a company.
    /// </summary>
    /// <param name="position">The position to add.</param>
    /// <exception cref="InvalidOperationException">Position already exists or company doesn't exist.</exception>
    public void AddPosition(JobPosition position)
    {
        if (position.IsNone)
        {
            throw new ArgumentException("Cannot add JobPosition.None.", nameof(position));
        }

        if (_positions.ContainsKey(position.Id))
        {
            throw new InvalidOperationException($"Job position {position.Id} already exists.");
        }

        var company = GetCompany(position.CompanyId);
        if (company is null)
        {
            throw new InvalidOperationException(
                $"Cannot add position {position.Id}: company {position.CompanyId} does not exist.");
        }

        _positions[position.Id] = position;
        company.AddPosition(position.Id);
    }

    /// <summary>
    /// Gets a job position by ID. Returns JobPosition.None if not found.
    /// </summary>
    public JobPosition GetPosition(JobPositionId positionId)
    {
        return _positions.TryGetValue(positionId, out var position) ? position : JobPosition.None;
    }

    /// <summary>
    /// Checks if a job position exists.
    /// </summary>
    public bool PositionExists(JobPositionId positionId) => _positions.ContainsKey(positionId);

    /// <summary>
    /// Removes a job position.
    /// </summary>
    /// <exception cref="InvalidOperationException">Position doesn't exist.</exception>
    public void RemovePosition(JobPositionId positionId)
    {
        if (positionId.IsNone)
        {
            throw new ArgumentException("Cannot remove JobPositionId.None.", nameof(positionId));
        }

        var position = GetPosition(positionId);
        if (position.IsNone)
        {
            throw new InvalidOperationException($"Job position {positionId} does not exist.");
        }

        var company = GetCompany(position.CompanyId);
        company?.RemovePosition(positionId);
        _positions.Remove(positionId);
    }

    /// <summary>
    /// Validates the integrity of the companies state.
    /// </summary>
    /// <param name="ledger">The money ledger to validate account ownership.</param>
    /// <returns>Validation result with any violations found.</returns>
    public CompaniesValidationResult ValidateIntegrity(MoneyLedger ledger)
    {
        ArgumentNullException.ThrowIfNull(ledger);

        var violations = new List<string>();

        // Validate treasury accounts
        foreach (var (companyId, company) in _companies)
        {
            if (!ledger.AccountExists(company.TreasuryAccountId))
            {
                violations.Add(
                    $"Company {companyId} has treasury account {company.TreasuryAccountId} that does not exist in ledger.");
                continue;
            }

            var owner = ledger.GetOwner(company.TreasuryAccountId);
            if (owner != companyId.EntityId)
            {
                violations.Add(
                    $"Company {companyId}'s treasury account {company.TreasuryAccountId} is owned by {owner}, expected {companyId.EntityId}.");
            }
        }

        // Validate positions belong to existing companies
        foreach (var (positionId, position) in _positions)
        {
            if (!CompanyExists(position.CompanyId))
            {
                violations.Add(
                    $"Job position {positionId} belongs to non-existent company {position.CompanyId}.");
            }
        }

        return violations.Count == 0
            ? CompaniesValidationResult.Success()
            : CompaniesValidationResult.Failure(violations);
    }

    /// <summary>
    /// Creates a deep copy of this state.
    /// </summary>
    public CompaniesState Clone()
    {
        var clone = new CompaniesState();

        // Clone companies (they have mutable position sets)
        foreach (var (companyId, company) in _companies)
        {
            var clonedCompany = new Company(company.Id, company.Name, company.TreasuryAccountId);
            foreach (var positionId in company.Positions)
            {
                clonedCompany.AddPosition(positionId);
            }
            clone._companies[companyId] = clonedCompany;
        }

        // Clone positions (structs are copied by value)
        foreach (var (positionId, position) in _positions)
        {
            clone._positions[positionId] = position;
        }

        return clone;
    }

    /// <summary>
    /// Restores a company from a snapshot without validation.
    /// Used by persistence layer to restore state.
    /// </summary>
    internal void RestoreCompany(Company company)
    {
        ArgumentNullException.ThrowIfNull(company);
        _companies[company.Id] = company;
    }

    /// <summary>
    /// Restores a job position from a snapshot without validation.
    /// Used by persistence layer to restore state.
    /// </summary>
    internal void RestorePosition(JobPosition position)
    {
        _positions[position.Id] = position;

        // Also add to company's position set
        if (_companies.TryGetValue(position.CompanyId, out var company))
        {
            company.AddPosition(position.Id);
        }
    }
}

/// <summary>
/// Validation result for companies state.
/// </summary>
public sealed class CompaniesValidationResult
{
    public bool IsValid { get; }
    public IReadOnlyList<string> Violations { get; }

    private CompaniesValidationResult(bool isValid, IReadOnlyList<string> violations)
    {
        IsValid = isValid;
        Violations = violations;
    }

    public static CompaniesValidationResult Success() => new(true, Array.Empty<string>());
    public static CompaniesValidationResult Failure(IReadOnlyList<string> violations) => new(false, violations);
}
