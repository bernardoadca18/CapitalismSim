using CapitalismSim.Simulation.Core.Companies;
using CapitalismSim.Simulation.Core.Entities;
using CapitalismSim.Simulation.Core.Economics;
using CapitalismSim.Simulation.Core.Population;

namespace CapitalismSim.Simulation.Core.Employment;

/// <summary>
/// Manages all employment contracts and payroll states in the simulation.
/// </summary>
/// <remarks>
/// Design invariants:
/// - Each employment contract has a unique EmploymentContractId
/// - A person can have multiple employment contracts (past and present)
///   but at most one active contract per company
/// - An active contract references a valid person, company, and job position
/// - Each contract has a corresponding payroll state
/// - A job position can have at most one active contract at a time
///
/// Wage Arrears:
/// - When a company cannot pay wages, they accrue as unpaid wages
/// - PayrollState tracks accrued wages and missed pay periods
/// - This creates a liability for companies and enables enforcement
/// </remarks>
public sealed class EmploymentState
{
    private readonly Dictionary<EmploymentContractId, EmploymentContract> _contracts = new();
    private readonly Dictionary<EmploymentContractId, PayrollState> _payrollStates = new();

    // Index for position occupancy: JobPositionId -> EmploymentContractId (active only)
    private readonly Dictionary<JobPositionId, EmploymentContractId> _positionOccupancy = new();

    /// <summary>Number of employment contracts.</summary>
    public int ContractCount => _contracts.Count;

    /// <summary>Number of active contracts.</summary>
    public int ActiveContractCount => _contracts.Values.Count(c => c.IsActive);

    /// <summary>Gets all contract IDs.</summary>
    public IEnumerable<EmploymentContractId> GetAllContractIds() => _contracts.Keys;

    /// <summary>
    /// Creates a new employment contract and initializes its payroll state.
    /// </summary>
    /// <param name="contract">The contract to create.</param>
    /// <param name="population">Population state to validate the person exists.</param>
    /// <param name="companies">Companies state to validate company and position.</param>
    /// <exception cref="InvalidOperationException">Validation failed.</exception>
    public void CreateContract(
        EmploymentContract contract,
        PopulationState population,
        CompaniesState companies)
    {
        ArgumentNullException.ThrowIfNull(population);
        ArgumentNullException.ThrowIfNull(companies);

        if (contract.IsNone)
        {
            throw new ArgumentException("Cannot create EmploymentContract.None.", nameof(contract));
        }

        if (_contracts.ContainsKey(contract.Id))
        {
            throw new InvalidOperationException($"Contract {contract.Id} already exists.");
        }

        // Validate person exists
        if (!population.PersonExists(contract.PersonId))
        {
            throw new InvalidOperationException(
                $"Cannot create contract {contract.Id}: person {contract.PersonId} does not exist.");
        }

        // Validate company exists
        if (!companies.CompanyExists(contract.CompanyId))
        {
            throw new InvalidOperationException(
                $"Cannot create contract {contract.Id}: company {contract.CompanyId} does not exist.");
        }

        // Validate position exists
        var position = companies.GetPosition(contract.JobPositionId);
        if (position.IsNone)
        {
            throw new InvalidOperationException(
                $"Cannot create contract {contract.Id}: position {contract.JobPositionId} does not exist.");
        }

        // Validate position belongs to the correct company
        if (position.CompanyId != contract.CompanyId)
        {
            throw new InvalidOperationException(
                $"Cannot create contract {contract.Id}: position {contract.JobPositionId} belongs to company {position.CompanyId}, not {contract.CompanyId}.");
        }

        // Enforce one active contract per position
        if (_positionOccupancy.TryGetValue(contract.JobPositionId, out var existingContractId))
        {
            throw new InvalidOperationException(
                $"Cannot create contract {contract.Id}: position {contract.JobPositionId} is already occupied by active contract {existingContractId}.");
        }

        // Create contract
        _contracts[contract.Id] = contract;

        // Track position occupancy for active contracts
        if (contract.IsActive)
        {
            _positionOccupancy[contract.JobPositionId] = contract.Id;
        }

        // Initialize payroll state with first pay day = start day + pay interval
        var payrollState = new PayrollState(contract.Id, contract.StartDayIndex + contract.PayIntervalDays);
        _payrollStates[contract.Id] = payrollState;
    }

    /// <summary>
    /// Gets a contract by ID. Returns EmploymentContract.None if not found.
    /// </summary>
    public EmploymentContract GetContract(EmploymentContractId contractId)
    {
        return _contracts.TryGetValue(contractId, out var contract) ? contract : EmploymentContract.None;
    }

    /// <summary>
    /// Checks if a contract exists.
    /// </summary>
    public bool ContractExists(EmploymentContractId contractId) => _contracts.ContainsKey(contractId);

    /// <summary>
    /// Terminates an active contract.
    /// </summary>
    /// <param name="contractId">The contract to terminate.</param>
    /// <param name="endDayIndex">The day the contract ends.</param>
    /// <exception cref="InvalidOperationException">Contract doesn't exist or is already terminated.</exception>
    public void TerminateContract(EmploymentContractId contractId, long endDayIndex)
    {
        if (contractId.IsNone)
        {
            throw new ArgumentException("Cannot terminate EmploymentContractId.None.", nameof(contractId));
        }

        var contract = GetContract(contractId);
        if (contract.IsNone)
        {
            throw new InvalidOperationException($"Contract {contractId} does not exist.");
        }

        if (!contract.IsActive)
        {
            throw new InvalidOperationException($"Contract {contractId} is already terminated.");
        }

        var terminatedContract = contract.WithEndDayIndex(endDayIndex);
        _contracts[contractId] = terminatedContract;

        // Release position occupancy
        _positionOccupancy.Remove(contract.JobPositionId);
    }

    /// <summary>
    /// Gets the payroll state for a contract. Returns null if not found.
    /// </summary>
    public PayrollState? GetPayrollState(EmploymentContractId contractId)
    {
        return _payrollStates.TryGetValue(contractId, out var state) ? state : null;
    }

    /// <summary>
    /// Gets all active contracts for a person.
    /// </summary>
    public IEnumerable<EmploymentContract> GetActiveContractsForPerson(PersonId personId)
    {
        return _contracts.Values.Where(c => c.PersonId == personId && c.IsActive);
    }

    /// <summary>
    /// Gets all active contracts for a company.
    /// </summary>
    public IEnumerable<EmploymentContract> GetActiveContractsForCompany(CompanyId companyId)
    {
        return _contracts.Values.Where(c => c.CompanyId == companyId && c.IsActive);
    }

    /// <summary>
    /// Gets total unpaid wages across all contracts for a company.
    /// </summary>
    public long GetTotalUnpaidWagesForCompany(CompanyId companyId)
    {
        long total = 0;
        foreach (var contractId in _contracts.Keys)
        {
            var contract = _contracts[contractId];
            if (contract.CompanyId == companyId && contract.IsActive)
            {
                var payroll = GetPayrollState(contractId);
                if (payroll is not null)
                {
                    total = checked(total + payroll.AccruedUnpaidWagesMinorUnits);
                }
            }
        }
        return total;
    }

    /// <summary>
    /// Validates the integrity of the employment state.
    /// </summary>
    public EmploymentValidationResult ValidateIntegrity(
        PopulationState population,
        CompaniesState companies)
    {
        ArgumentNullException.ThrowIfNull(population);
        ArgumentNullException.ThrowIfNull(companies);

        var violations = new List<string>();

        foreach (var (contractId, contract) in _contracts)
        {
            // Validate person exists
            if (!population.PersonExists(contract.PersonId))
            {
                violations.Add(
                    $"Contract {contractId} references non-existent person {contract.PersonId}.");
            }

            // Validate company exists
            if (!companies.CompanyExists(contract.CompanyId))
            {
                violations.Add(
                    $"Contract {contractId} references non-existent company {contract.CompanyId}.");
            }

            // Validate position exists
            if (!companies.PositionExists(contract.JobPositionId))
            {
                violations.Add(
                    $"Contract {contractId} references non-existent position {contract.JobPositionId}.");
            }

            // Validate payroll state exists
            if (!_payrollStates.ContainsKey(contractId))
            {
                violations.Add(
                    $"Contract {contractId} has no payroll state.");
            }
        }

        return violations.Count == 0
            ? EmploymentValidationResult.Success()
            : EmploymentValidationResult.Failure(violations);
    }

    /// <summary>
    /// Creates a deep copy of this state.
    /// </summary>
    public EmploymentState Clone()
    {
        var clone = new EmploymentState();

        // Clone contracts (structs are copied by value)
        foreach (var (contractId, contract) in _contracts)
        {
            clone._contracts[contractId] = contract;
        }

        // Clone payroll states (classes with mutable state)
        foreach (var (contractId, payroll) in _payrollStates)
        {
            clone._payrollStates[contractId] = payroll.Clone();
        }

        // Clone position occupancy index
        foreach (var (positionId, contractId) in _positionOccupancy)
        {
            clone._positionOccupancy[positionId] = contractId;
        }

        return clone;
    }

    /// <summary>
    /// Restores a contract from a snapshot without validation.
    /// Used by persistence layer to restore state.
    /// </summary>
    internal void RestoreContract(EmploymentContract contract)
    {
        _contracts[contract.Id] = contract;

        // Initialize payroll state if not already present
        if (!_payrollStates.ContainsKey(contract.Id))
        {
            _payrollStates[contract.Id] = new PayrollState(contract.Id, contract.StartDayIndex + contract.PayIntervalDays);
        }

        // Track position occupancy for active contracts
        if (contract.IsActive)
        {
            _positionOccupancy[contract.JobPositionId] = contract.Id;
        }
    }
}

/// <summary>
/// Validation result for employment state.
/// </summary>
public sealed class EmploymentValidationResult
{
    public bool IsValid { get; }
    public IReadOnlyList<string> Violations { get; }

    private EmploymentValidationResult(bool isValid, IReadOnlyList<string> violations)
    {
        IsValid = isValid;
        Violations = violations;
    }

    public static EmploymentValidationResult Success() => new(true, Array.Empty<string>());
    public static EmploymentValidationResult Failure(IReadOnlyList<string> violations) => new(false, violations);
}
