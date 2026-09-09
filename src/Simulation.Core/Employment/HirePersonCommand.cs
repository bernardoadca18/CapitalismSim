using CapitalismSim.Simulation.Core.Companies;
using CapitalismSim.Simulation.Core.Economics;
using CapitalismSim.Simulation.Core.Entities;
using CapitalismSim.Simulation.Core.Population;

namespace CapitalismSim.Simulation.Core.Employment;

/// <summary>
/// Command to hire a person for a job position.
/// </summary>
/// <remarks>
/// This command initiates the hiring of a person for a job position.
/// The contract identity is allocated internally by the simulation engine's
/// global allocator rather than provided by the caller.
///
/// Invariants validated atomically before mutation:
/// - Person exists, personal account exists, and personal account is owned by person
/// - Job position exists, company exists, and position belongs to company
/// - Company treasury account exists and is owned by company
/// - Position is not already occupied by an active contract
/// - Compensation is valid (> 0) and currency matches ledger currency
/// - StartDayIndex is non-negative
/// </remarks>
public sealed record HirePersonCommand : Commands.ISimulationCommand
{
    /// <summary>The person to hire.</summary>
    public required PersonId PersonId { get; init; }

    /// <summary>The job position to fill.</summary>
    public required JobPositionId JobPositionId { get; init; }

    /// <summary>The simulation day the contract starts.</summary>
    public required long StartDayIndex { get; init; }
}

/// <summary>
/// Handles HirePersonCommand by performing atomic hiring operations.
/// </summary>
public sealed class HirePersonCommandHandler : Commands.ISimulationCommandHandler<HirePersonCommand>
{
    private readonly PopulationState _population;
    private readonly CompaniesState _companies;
    private readonly EmploymentState _employment;
    private readonly MoneyLedger _ledger;
    private readonly EntityIdAllocator _entityIds;

    /// <summary>
    /// Creates a new handler with the required state and allocator dependencies.
    /// </summary>
    public HirePersonCommandHandler(
        PopulationState population,
        CompaniesState companies,
        EmploymentState employment,
        MoneyLedger ledger,
        EntityIdAllocator entityIds)
    {
        _population = population ?? throw new ArgumentNullException(nameof(population));
        _companies = companies ?? throw new ArgumentNullException(nameof(companies));
        _employment = employment ?? throw new ArgumentNullException(nameof(employment));
        _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        _entityIds = entityIds ?? throw new ArgumentNullException(nameof(entityIds));
    }

    /// <summary>
    /// Executes the hire operation atomically, allocating an EmploymentContractId and returning it.
    /// </summary>
    public EmploymentContractId Execute(HirePersonCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.StartDayIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(command), "Start day index cannot be negative.");
        }

        if (command.PersonId.IsNone)
        {
            throw new ArgumentException("Person ID cannot be None.", nameof(command));
        }

        if (command.JobPositionId.IsNone)
        {
            throw new ArgumentException("Job position ID cannot be None.", nameof(command));
        }

        // 1. Person exists
        var person = _population.GetPerson(command.PersonId);
        if (person.IsNone)
        {
            throw new InvalidOperationException(
                $"Cannot hire: person {command.PersonId} does not exist.");
        }

        // 2. Personal account exists and is owned by the person
        if (person.PersonalAccountId.IsNone)
        {
            throw new InvalidOperationException(
                $"Cannot hire: person {command.PersonId} has no personal account.");
        }

        if (!_ledger.AccountExists(person.PersonalAccountId))
        {
            throw new InvalidOperationException(
                $"Cannot hire: personal account {person.PersonalAccountId} for person {command.PersonId} does not exist in ledger.");
        }

        var personalAccountOwner = _ledger.GetOwner(person.PersonalAccountId);
        if (personalAccountOwner != person.Id.EntityId)
        {
            throw new InvalidOperationException(
                $"Cannot hire: personal account {person.PersonalAccountId} is owned by {personalAccountOwner}, expected {person.Id.EntityId}.");
        }

        // 3. Position exists
        var position = _companies.GetPosition(command.JobPositionId);
        if (position.IsNone)
        {
            throw new InvalidOperationException(
                $"Cannot hire: position {command.JobPositionId} does not exist.");
        }

        // 4. Company exists
        var company = _companies.GetCompany(position.CompanyId);
        if (company is null)
        {
            throw new InvalidOperationException(
                $"Cannot hire: company {position.CompanyId} for position {command.JobPositionId} does not exist.");
        }

        // 5. Company treasury account exists and is owned by company
        if (company.TreasuryAccountId.IsNone)
        {
            throw new InvalidOperationException(
                $"Cannot hire: company {company.Id} has no treasury account.");
        }

        if (!_ledger.AccountExists(company.TreasuryAccountId))
        {
            throw new InvalidOperationException(
                $"Cannot hire: treasury account {company.TreasuryAccountId} for company {company.Id} does not exist in ledger.");
        }

        var treasuryOwner = _ledger.GetOwner(company.TreasuryAccountId);
        if (treasuryOwner != company.Id.EntityId)
        {
            throw new InvalidOperationException(
                $"Cannot hire: treasury account {company.TreasuryAccountId} is owned by {treasuryOwner}, expected {company.Id.EntityId}.");
        }

        // 6. Position is not occupied
        if (_employment.IsPositionOccupied(command.JobPositionId))
        {
            throw new InvalidOperationException(
                $"Cannot hire: position {command.JobPositionId} is already occupied.");
        }

        // 7. Compensation valid and currency matches ledger currency
        if (!position.Compensation.IsValidForPosition)
        {
            throw new InvalidOperationException(
                $"Cannot hire: position {command.JobPositionId} has invalid compensation terms.");
        }

        if (position.Compensation.Currency != _ledger.Currency)
        {
            throw new InvalidOperationException(
                $"Cannot hire: position compensation currency {position.Compensation.Currency} does not match ledger currency {_ledger.Currency}.");
        }

        // All validations passed! Now allocate ID and create contract + payroll state + occupancy atomically.
        var contractEntityId = _entityIds.Allocate();
        var contractId = new EmploymentContractId(contractEntityId);

        var contract = new EmploymentContract(
            contractId,
            command.PersonId,
            position.CompanyId,
            command.JobPositionId,
            command.StartDayIndex,
            position.Compensation);

        _employment.CreateContract(contract, _population, _companies);

        return contractId;
    }

    /// <summary>
    /// Handles the HirePersonCommand.
    /// </summary>
    public void Handle(HirePersonCommand command)
    {
        Execute(command);
    }
}
