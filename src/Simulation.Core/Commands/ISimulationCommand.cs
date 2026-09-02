namespace CapitalismSim.Simulation.Core.Commands;

/// <summary>
/// Marker interface for a request to change simulation state, dispatched
/// synchronously through <see cref="CommandDispatcher"/>. No concrete
/// gameplay commands (such as a future "BuyPropertyCommand") are defined in
/// this phase — this is purely the dispatch infrastructure, exercised by
/// probe commands in tests and the headless runner.
/// </summary>
public interface ISimulationCommand
{
}

/// <summary>
/// Handles exactly one type of <see cref="ISimulationCommand"/>.
/// </summary>
/// <typeparam name="TCommand">The concrete command type this handler processes.</typeparam>
public interface ISimulationCommandHandler<in TCommand>
    where TCommand : ISimulationCommand
{
    void Handle(TCommand command);
}
