using CapitalismSim.Simulation.Core.Commands;

namespace CapitalismSim.Simulation.Headless.Probe;

/// <summary>
/// A diagnostic-only command used exclusively to exercise the command
/// dispatch infrastructure end-to-end. Not a real gameplay concept.
/// </summary>
public sealed record AllocateProbeEntityCommand : ISimulationCommand;

/// <summary>Handles <see cref="AllocateProbeEntityCommand"/> by allocating an entity id and recording it.</summary>
public sealed class AllocateProbeEntityCommandHandler : ISimulationCommandHandler<AllocateProbeEntityCommand>
{
    private readonly Core.Entities.EntityIdAllocator _allocator;
    private readonly List<Core.Entities.EntityId> _allocated = new();

    public AllocateProbeEntityCommandHandler(Core.Entities.EntityIdAllocator allocator)
    {
        _allocator = allocator;
    }

    /// <summary>Every entity id allocated so far via this handler, in allocation order.</summary>
    public IReadOnlyList<Core.Entities.EntityId> Allocated => _allocated;

    public void Handle(AllocateProbeEntityCommand command)
    {
        _allocated.Add(_allocator.Allocate());
    }
}
