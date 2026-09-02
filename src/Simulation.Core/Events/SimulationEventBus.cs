using CapitalismSim.Simulation.Core.Time;

namespace CapitalismSim.Simulation.Core.Events;

/// <summary>
/// A minimal synchronous, in-process publish/subscribe bus for simulation
/// events. Subscribers for a given event type are invoked immediately, in
/// registration order, when an event of that type is published.
/// </summary>
/// <remarks>
/// This is not an event-sourcing framework and does not persist events by
/// itself; it is a same-process notification mechanism. Persisting
/// meaningful history (if ever needed) is a decision for the domain that
/// needs it, made in a later phase.
/// </remarks>
public sealed class SimulationEventBus
{
    private readonly Dictionary<Type, List<Action<ISimulationEvent>>> _subscribersByEventType = new();
    private long _nextSequenceNumber = 1;
    private long _totalPublishedCount;

    /// <summary>Total number of events published through this bus so far.</summary>
    public long TotalPublishedCount => _totalPublishedCount;

    /// <summary>The next sequence number that will be assigned to a published event.</summary>
    public long NextSequenceNumber => _nextSequenceNumber;

    /// <summary>Restores the sequence number counter from a captured snapshot.</summary>
    public void RestoreSequenceNumber(long nextSequenceNumber)
    {
        if (nextSequenceNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(nextSequenceNumber), "Sequence number must be at least 1.");
        }
        _nextSequenceNumber = nextSequenceNumber;
    }

    /// <summary>
    /// Subscribes to events of type <typeparamref name="TEvent"/>. Multiple
    /// subscribers are allowed and are invoked in the order they
    /// subscribed.
    /// </summary>
    public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : ISimulationEvent
    {
        ArgumentNullException.ThrowIfNull(handler);

        Type eventType = typeof(TEvent);
        if (!_subscribersByEventType.TryGetValue(eventType, out List<Action<ISimulationEvent>>? list))
        {
            list = new List<Action<ISimulationEvent>>();
            _subscribersByEventType[eventType] = list;
        }

        list.Add(evt => handler((TEvent)evt));
    }

    /// <summary>
    /// Publishes an event at the given simulation instant. Assigns the next
    /// monotonically increasing sequence number and immediately invokes any
    /// subscribers registered for its concrete type.
    /// </summary>
    public PublishedEvent Publish<TEvent>(TEvent simulationEvent, SimulationInstant at) where TEvent : ISimulationEvent
    {
        ArgumentNullException.ThrowIfNull(simulationEvent);

        var published = new PublishedEvent(simulationEvent, at, _nextSequenceNumber);
        _nextSequenceNumber = checked(_nextSequenceNumber + 1);
        _totalPublishedCount = checked(_totalPublishedCount + 1);

        Type eventType = typeof(TEvent);
        if (_subscribersByEventType.TryGetValue(eventType, out List<Action<ISimulationEvent>>? list))
        {
            // Snapshot count to avoid issues if a handler subscribes more
            // handlers during dispatch (not disallowed, but should not
            // affect the current dispatch pass).
            int count = list.Count;
            for (int i = 0; i < count; i++)
            {
                list[i](simulationEvent);
            }
        }

        return published;
    }
}
