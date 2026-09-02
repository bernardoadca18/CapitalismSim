namespace CapitalismSim.Simulation.Core.Entities;

/// <summary>
/// Allocates unique <see cref="EntityId"/> values within a single
/// simulation world, using simple monotonically increasing integers.
/// </summary>
/// <remarks>
/// This is not thread-safe by design, consistent with the kernel's
/// single-threaded execution model for this phase (see
/// docs/adr/0003-deterministic-single-threaded-kernel.md). Its entire state
/// is the next value to hand out, which makes snapshotting trivial.
/// </remarks>
public sealed class EntityIdAllocator
{
    private long _nextValue;

    public EntityIdAllocator(long nextValue = 1)
    {
        if (nextValue < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(nextValue),
                nextValue,
                "The next allocated id must be at least 1; zero is reserved for EntityId.None.");
        }

        _nextValue = nextValue;
    }

    /// <summary>The value that will be handed out by the next call to <see cref="Allocate"/>.</summary>
    public long NextValue => _nextValue;

    /// <summary>Allocates and returns a new, unique <see cref="EntityId"/>.</summary>
    public EntityId Allocate()
    {
        long value = _nextValue;
        _nextValue = checked(_nextValue + 1);
        return new EntityId(value);
    }

    /// <summary>
    /// Restores the allocator's next-value counter, for use only when
    /// loading a snapshot.
    /// </summary>
    public void Restore(long nextValue)
    {
        if (nextValue < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(nextValue),
                nextValue,
                "The next allocated id must be at least 1.");
        }

        _nextValue = nextValue;
    }
}
