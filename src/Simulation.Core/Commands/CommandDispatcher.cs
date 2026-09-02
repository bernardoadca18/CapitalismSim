namespace CapitalismSim.Simulation.Core.Commands;

/// <summary>
/// A minimal, synchronous, in-process command dispatcher. Exactly one
/// handler may be registered per command type; this is an explicit design
/// choice (see project brief section 26/40) rather than a "first wins" or
/// "fan out to all" policy, because ambiguous handling of a state-changing
/// command would be a correctness hazard for a deterministic simulation.
/// </summary>
/// <remarks>
/// This is intentionally not a generic mediator framework: there is no
/// pipeline, no middleware, and no reflection-based handler discovery.
/// Handlers are registered explicitly by calling code.
/// </remarks>
public sealed class CommandDispatcher
{
    private readonly Dictionary<Type, object> _handlersByCommandType = new();
    private long _totalDispatchCount;

    /// <summary>Total number of commands dispatched through this dispatcher so far.</summary>
    public long TotalDispatchCount => _totalDispatchCount;

    /// <summary>
    /// Registers the handler for <typeparamref name="TCommand"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// A handler for <typeparamref name="TCommand"/> is already registered.
    /// </exception>
    public void RegisterHandler<TCommand>(ISimulationCommandHandler<TCommand> handler)
        where TCommand : ISimulationCommand
    {
        ArgumentNullException.ThrowIfNull(handler);

        Type commandType = typeof(TCommand);
        if (!_handlersByCommandType.TryAdd(commandType, handler))
        {
            throw new InvalidOperationException(
                $"A handler for command type '{commandType.Name}' is already registered. " +
                "Exactly one handler per command type is supported by design.");
        }
    }

    /// <summary>
    /// Dispatches <paramref name="command"/> synchronously to its
    /// registered handler.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// No handler is registered for <typeparamref name="TCommand"/>.
    /// </exception>
    public void Dispatch<TCommand>(TCommand command)
        where TCommand : ISimulationCommand
    {
        ArgumentNullException.ThrowIfNull(command);

        _totalDispatchCount = checked(_totalDispatchCount + 1);

        Type commandType = typeof(TCommand);
        if (!_handlersByCommandType.TryGetValue(commandType, out object? handlerObj))
        {
            throw new InvalidOperationException(
                $"No handler is registered for command type '{commandType.Name}'.");
        }

        var handler = (ISimulationCommandHandler<TCommand>)handlerObj;
        handler.Handle(command);
    }

    /// <summary>Returns whether a handler is registered for <typeparamref name="TCommand"/>.</summary>
    public bool HasHandler<TCommand>() where TCommand : ISimulationCommand
        => _handlersByCommandType.ContainsKey(typeof(TCommand));
}
