using CapitalismSim.Simulation.Core.Entities;
using Xunit;

namespace CapitalismSim.Simulation.Core.Tests.Entities;

public sealed class EntityIdAllocatorTests
{
    [Fact]
    public void Allocate_ProducesSequentialIds()
    {
        var allocator = new EntityIdAllocator();

        EntityId first = allocator.Allocate();
        EntityId second = allocator.Allocate();
        EntityId third = allocator.Allocate();

        Assert.Equal(1, first.Value);
        Assert.Equal(2, second.Value);
        Assert.Equal(3, third.Value);
    }

    [Fact]
    public void Allocate_NeverProducesDuplicates()
    {
        var allocator = new EntityIdAllocator();
        var seen = new HashSet<EntityId>();

        for (int i = 0; i < 10_000; i++)
        {
            EntityId id = allocator.Allocate();
            Assert.True(seen.Add(id), $"Duplicate id allocated: {id}");
        }
    }

    [Fact]
    public void Allocate_NeverProducesNone()
    {
        var allocator = new EntityIdAllocator();

        for (int i = 0; i < 100; i++)
        {
            Assert.False(allocator.Allocate().IsNone);
        }
    }

    [Fact]
    public void Restore_ThenAllocate_ContinuesFromRestoredValue()
    {
        var allocator = new EntityIdAllocator();
        allocator.Allocate();
        allocator.Allocate();

        long snapshotNextValue = allocator.NextValue;

        var restored = new EntityIdAllocator();
        restored.Restore(snapshotNextValue);

        Assert.Equal(allocator.Allocate(), restored.Allocate());
    }

    [Fact]
    public void Constructor_RejectsValueBelowOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EntityIdAllocator(0));
    }

    [Fact]
    public void Restore_RejectsValueBelowOne()
    {
        var allocator = new EntityIdAllocator();
        Assert.Throws<ArgumentOutOfRangeException>(() => allocator.Restore(0));
    }

    [Fact]
    public void EntityId_EqualityAndComparison_AreValueBased()
    {
        var a = new EntityId(5);
        var b = new EntityId(5);
        var c = new EntityId(6);

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.True(a < c);
        Assert.True(c > a);
    }
}
