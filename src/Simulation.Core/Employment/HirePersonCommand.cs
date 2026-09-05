using CapitalismSim.Simulation.Core.Companies;
using CapitalismSim.Simulation.Core.Entities;
using CapitalismSim.Simulation.Core.Population;

namespace CapitalismSim.Simulation.Core.Employment;

/// <summary>
/// Command to hire a person for a job position.
/// </summary>
/// <remarks>
/// This command creates an employment contract linking a person to a job position.
/// It validates that:
/// - The person exists in PopulationState
/// - The company exists in CompaniesState
/// - The job position exists and belongs to the company
/// - The position is not already occupied by an active contract
/// </remarks>
public sealed record HirePersonCommand : Commands.ISimulationCommand
{
    /// <summary>The unique ID for this employment contract.</summary>
    public required EmploymentContractId ContractId { get; init; }

    /// <summary>The person to hire.</summary>
    public required PersonId PersonId { get; init; }

    /// <summary>The company doing the hiring.</summary>
    public required CompanyId CompanyId { get; init; }

    /// <summary>The job position to fill.</summary>
    public required JobPositionId JobPositionId { get; init; }

    /// <summary>The simulation day the contract starts.</summary>
    public required long StartDayIndex { get; init; }
}

/// <summary>
/// Handles HirePersonCommand by creating an employment contract.
/// </summary>
public sealed class HirePersonCommandHandler : Commands.ISimulationCommandHandler<HirePersonCommand>
{
    private readonly PopulationState _population;
    private readonly CompaniesState _companies;
    private readonly EmploymentState _employment;

    /// <summary>
    /// Creates a new handler with the required state dependencies.
    /// </summary>
    public HirePersonCommandHandler(
        PopulationState population,
        CompaniesState companies,
        EmploymentState employment)
    {
        _population = population ?? throw new ArgumentNullException(nameof(population));
        _companies = companies ?? throw new ArgumentNullException(nameof(companies));
        _employment = employment ?? throw new ArgumentNullException(nameof(employment));
    }

    /// <summary>
    /// Handles the HirePersonCommand.
    /// </summary>
    public void Handle(HirePersonCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Get the position to extract compensation
        var position = _companies.GetPosition(command.JobPositionId);
        if (position.IsNone)
        {
            throw new InvalidOperationException(
                $"Cannot hire: position {command.JobPositionId} does not exist.");
        }

        // Create the contract with compensation copied from position
        var contract = new EmploymentContract(
            command.ContractId,
            command.PersonId,
            command.CompanyId,
            command.JobPositionId,
            command.StartDayIndex,
            position.Compensation);

        // CreateContract validates all invariants and throws on failure
        _employment.CreateContract(contract, _population, _companies);
    }
}
