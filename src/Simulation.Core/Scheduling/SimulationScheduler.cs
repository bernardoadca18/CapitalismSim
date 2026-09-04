using CapitalismSim.Simulation.Core.Time;

namespace CapitalismSim.Simulation.Core.Scheduling;

/// <summary>
/// A deterministic, single-threaded scheduler that invokes registered
/// <see cref="ISimulationSystem"/> instances on their own fixed-interval
/// cadences as the simulation clock advances.
/// </summary>
/// <remarks>
/// <para>
/// <b>Determinism and ordering.</b> When the clock advances across a range
/// that contains occurrences for more than one system, occurrences are
/// executed in a single, explicit, stable order: primarily by scheduled
/// instant (earliest first), and for occurrences that land on the exact
/// same instant, by the order in which the systems were registered with
/// this scheduler. This is documented and intentional — it does not depend
/// on hash table enumeration order or any other incidental ordering.
/// </para>
/// <para>
/// <b>Streaming execution.</b> This scheduler uses a k-way merge with a
/// min-heap to process occurrences in order without materializing all due
/// occurrences upfront. Memory usage is O(S) where S is the number of
/// registered systems, not O(E) where E is the number of due occurrences.
/// Time complexity is O(E log S).
/// </para>
/// <para>
/// <b>Threading.</b> This type is not thread-safe. The kernel is
/// single-threaded by design in this phase; see
/// docs/adr/0003-deterministic-single-threaded-kernel.md.
/// </para>
/// <para>
/// <b>No ticks are lost or duplicated.</b> Every occurrence whose scheduled
/// instant falls within (previousClockInstant, newClockInstant] fires
/// exactly once, regardless of how large the advance is or how many
/// distinct systems are registered.
/// </para>
/// </remarks>
public sealed class SimulationScheduler
{
    private readonly List<RegisteredSystem> _systems = new();
    private readonly Dictionary<SimulationSystemId, int> _idToIndex = new();

    // Min-heap for streaming k-way merge. Reused across calls to avoid
    // allocations. Each entry represents the next due occurrence for a system.
    // At most S entries exist at any time (one per system with due occurrences).
    private readonly List<HeapEntry> _heap = new();

    // Once the scheduler has executed at least one occurrence, registration is sealed.
    // This prevents adding systems mid-simulation which would break determinism guarantees.
    private bool _isSealed;

    /// <summary>Registers a system with a fixed periodic cadence.</summary>
    /// <exception cref="ArgumentException">A system with the same id is already registered.</exception>
    /// <exception cref="InvalidOperationException">The scheduler has already started executing occurrences.</exception>
    public void Register(ISimulationSystem system, SchedulingPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(system);

        if (_isSealed)
        {
            throw new InvalidOperationException(
                "Cannot register systems after the scheduler has started executing occurrences. " +
                "All systems must be registered before the first advance.");
        }

        if (_idToIndex.ContainsKey(system.Id))
        {
            throw new ArgumentException(
                $"A system with id '{system.Id}' is already registered.",
                nameof(system));
        }

        int registrationOrder = _systems.Count;
        _idToIndex[system.Id] = registrationOrder;
        _systems.Add(new RegisteredSystem(system, policy, registrationOrder));
    }

    /// <summary>The number of currently registered systems.</summary>
    public int SystemCount => _systems.Count;

    /// <summary>
    /// Executes every due occurrence for every registered system whose
    /// scheduled instant falls within (<paramref name="previousInstant"/>,
    /// <paramref name="newInstant"/>].
    /// </summary>
    /// <param name="previousInstant">The instant before the advance (exclusive bound).</param>
    /// <param name="newInstant">The target instant of the advance (inclusive bound).</param>
    /// <param name="clock">
    /// The simulation clock to advance before each tick. Before each call to
    /// <see cref="ISimulationSystem.Tick"/>, the clock is advanced to that
    /// occurrence's scheduled instant, ensuring <see cref="SimulationClock.Current"/>
    /// equals <see cref="SimulationTickContext.ScheduledInstant"/> during the tick.
    /// </param>
    /// <param name="state">
    /// The root aggregate for all simulation domain state, passed to each system tick.
    /// </param>
    /// <returns>The number of occurrences executed.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Exception behavior:</strong> If a system's <see cref="ISimulationSystem.Tick"/>
    /// method throws, the exception propagates immediately to the caller. At the point of
    /// the exception, <paramref name="clock"/>.Current will be at the scheduled instant of
    /// the failed tick (not the target instant). The scheduler remains in a valid state
    /// and can be used for subsequent advances.
    /// </para>
    /// <para>
    /// This is intentionally <em>not</em> transactional: ticks that completed before the
    /// exception are not rolled back. This design choice keeps the kernel simple and
    /// deterministic. Transactional semantics would be a future consideration if required.
    /// </para>
    /// </remarks>
    public long RunDueOccurrences(
        SimulationInstant previousInstant,
        SimulationInstant newInstant,
        SimulationClock clock,
        State.SimulationState state)
    {
        if (newInstant < previousInstant)
        {
            throw new ArgumentException("newInstant must not be before previousInstant.", nameof(newInstant));
        }

        // Seal registration on first call to prevent mid-simulation system additions.
        _isSealed = true;

        // Build initial heap: one entry per system that has at least one due occurrence.
        _heap.Clear();

        for (int systemIndex = 0; systemIndex < _systems.Count; systemIndex++)
        {
            RegisteredSystem entry = _systems[systemIndex];
            SchedulingPolicy policy = entry.Policy;

            // Find the first occurrence index > previousInstant.
            long lastIndexBeforeWindow = policy.OccurrenceIndexAtOrBefore(previousInstant);
            long firstDueIndex = lastIndexBeforeWindow + 1;

            // Check if this system has any occurrences in the window.
            long lastIndexAtOrBeforeNew = policy.OccurrenceIndexAtOrBefore(newInstant);
            if (firstDueIndex <= lastIndexAtOrBeforeNew)
            {
                SimulationInstant firstInstant = policy.InstantOfOccurrence(firstDueIndex);
                _heap.Add(new HeapEntry(
                    firstInstant,
                    entry.RegistrationOrder,
                    systemIndex,
                    firstDueIndex,
                    lastIndexAtOrBeforeNew));
            }
        }

        if (_heap.Count == 0)
        {
            return 0;
        }

        // Heapify: build min-heap from initial entries.
        // Start from last non-leaf node and sift down each.
        for (int i = (_heap.Count >> 1) - 1; i >= 0; i--)
        {
            SiftDown(i);
        }

        long executedCount = 0;

        // Process occurrences in order by extracting minimum each iteration.
        while (_heap.Count > 0)
        {
            // Extract the minimum entry (next due occurrence across all systems).
            HeapEntry min = _heap[0];

            // Advance the clock to the scheduled instant BEFORE executing the tick.
            // This ensures SimulationClock.Current equals SimulationTickContext.ScheduledInstant during the tick.
            clock.AdvanceTo(min.ScheduledInstant);

            // Execute the tick.
            long occurrenceNumber = min.CurrentOccurrenceIndex + 1; // 1-based
            var context = new SimulationTickContext(min.ScheduledInstant, occurrenceNumber, state);
            _systems[min.SystemIndex].System.Tick(in context);
            executedCount++;

            // Advance this system's cursor.
            long nextIndex = min.CurrentOccurrenceIndex + 1;

            if (nextIndex <= min.LastOccurrenceIndexInWindow)
            {
                // More occurrences for this system. Update entry with next occurrence.
                SimulationInstant nextInstant = _systems[min.SystemIndex].Policy.InstantOfOccurrence(nextIndex);
                _heap[0] = new HeapEntry(
                    nextInstant,
                    min.RegistrationOrder,
                    min.SystemIndex,
                    nextIndex,
                    min.LastOccurrenceIndexInWindow);

                // Restore heap property.
                SiftDown(0);
            }
            else
            {
                // No more occurrences for this system in this window. Remove from heap.
                int lastIndex = _heap.Count - 1;
                if (lastIndex > 0)
                {
                    _heap[0] = _heap[lastIndex];
                    _heap.RemoveAt(lastIndex);
                    SiftDown(0);
                }
                else
                {
                    _heap.RemoveAt(0);
                }
            }
        }

        return executedCount;
    }

    /// <summary>
    /// Restores the min-heap property by sifting the element at <paramref name="index"/>
    /// down to its correct position.
    /// </summary>
    private void SiftDown(int index)
    {
        int count = _heap.Count;
        while (true)
        {
            int leftChild = (index << 1) + 1;
            if (leftChild >= count)
            {
                break;
            }

            int rightChild = leftChild + 1;
            int smallestChild = leftChild;

            if (rightChild < count && CompareHeapEntries(_heap[rightChild], _heap[leftChild]) < 0)
            {
                smallestChild = rightChild;
            }

            if (CompareHeapEntries(_heap[index], _heap[smallestChild]) <= 0)
            {
                break;
            }

            // Swap.
            HeapEntry temp = _heap[index];
            _heap[index] = _heap[smallestChild];
            _heap[smallestChild] = temp;

            index = smallestChild;
        }
    }

    /// <summary>
    /// Compares two heap entries by (ScheduledInstant, RegistrationOrder) for min-heap ordering.
    /// </summary>
    private static int CompareHeapEntries(HeapEntry a, HeapEntry b)
    {
        int byInstant = a.ScheduledInstant.CompareTo(b.ScheduledInstant);
        return byInstant != 0 ? byInstant : a.RegistrationOrder.CompareTo(b.RegistrationOrder);
    }

    private readonly record struct RegisteredSystem(
        ISimulationSystem System,
        SchedulingPolicy Policy,
        int RegistrationOrder);

    /// <summary>
    /// An entry in the min-heap representing the next due occurrence for a system.
    /// </summary>
    private readonly record struct HeapEntry(
        SimulationInstant ScheduledInstant,
        int RegistrationOrder,
        int SystemIndex,
        long CurrentOccurrenceIndex,
        long LastOccurrenceIndexInWindow);
}
