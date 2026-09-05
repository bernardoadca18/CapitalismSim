using CapitalismSim.Simulation.Core.Entities;

namespace CapitalismSim.Simulation.Core.Employment;

/// <summary>
/// Command to terminate an active employment contract.
/// </summary>
/// <remarks>
/// This command terminates an active employment contract.
/// It validates that:
/// - The contract exists
/// - The contract is currently active (not already terminated)
///
/// After termination, the job position becomes available for new hires.
/// </remarks>
public sealed record TerminateEmploymentCommand : Commands.ISimulationCommand
{
    /// <summary>The contract to terminate.</summary>
    public required EmploymentContractId ContractId { get; init; }

    /// <summary>The simulation day the contract ends.</summary>
    public required long EndDayIndex { get; init; }
}

/// <summary>
/// Handles TerminateEmploymentCommand by ending an employment contract.
/// </summary>
public sealed class TerminateEmploymentCommandHandler : Commands.ISimulationCommandHandler<TerminateEmploymentCommand>
{
    private readonly EmploymentState _employment;

    /// <summary>
    /// Creates a new handler with the required state dependency.
    /// </summary>
    public TerminateEmploymentCommandHandler(EmploymentState employment)
    {
        _employment = employment ?? throw new ArgumentNullException(nameof(employment));
    }

    /// <summary>
    /// Handles the TerminateEmploymentCommand.
    /// </summary>
    public void Handle(TerminateEmploymentCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        // TerminateContract validates all invariants and throws on failure
        _employment.TerminateContract(command.ContractId, command.EndDayIndex);
    }
}
