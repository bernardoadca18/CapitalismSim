using CapitalismSim.Simulation.Core.Events;
using CapitalismSim.Simulation.Core.Time;
using Xunit;

namespace CapitalismSim.Simulation.Core.Tests.Events;

public sealed class SimulationEventBusTests
{
    private sealed record TestEvent(int Value) : ISimulationEvent;

    private sealed record OtherEvent(string Text) : ISimulationEvent;

    private static readonly int[] s_expected7 = [7];
    private static readonly string[] s_expectedFirstSecond = ["first", "second"];

    [Fact]
    public void Publish_InvokesSubscriber()
    {
        var bus = new SimulationEventBus();
        var received = new List<int>();
        bus.Subscribe<TestEvent>(e => received.Add(e.Value));

        bus.Publish(new TestEvent(7), SimulationInstant.Epoch);

        Assert.Equal(s_expected7, received);
    }

    [Fact]
    public void Publish_OnlyInvokesSubscribersOfMatchingType()
    {
        var bus = new SimulationEventBus();
        var testReceived = new List<int>();
        var otherReceived = new List<string>();

        bus.Subscribe<TestEvent>(e => testReceived.Add(e.Value));
        bus.Subscribe<OtherEvent>(e => otherReceived.Add(e.Text));

        bus.Publish(new TestEvent(1), SimulationInstant.Epoch);

        Assert.Single(testReceived);
        Assert.Empty(otherReceived);
    }

    [Fact]
    public void Publish_InvokesMultipleSubscribersInRegistrationOrder()
    {
        var bus = new SimulationEventBus();
        var order = new List<string>();

        bus.Subscribe<TestEvent>(_ => order.Add("first"));
        bus.Subscribe<TestEvent>(_ => order.Add("second"));

        bus.Publish(new TestEvent(1), SimulationInstant.Epoch);

        Assert.Equal(s_expectedFirstSecond, order);
    }

    [Fact]
    public void Publish_AssignsMonotonicallyIncreasingSequenceNumbers()
    {
        var bus = new SimulationEventBus();

        PublishedEvent first = bus.Publish(new TestEvent(1), SimulationInstant.Epoch);
        PublishedEvent second = bus.Publish(new TestEvent(2), SimulationInstant.Epoch);
        PublishedEvent third = bus.Publish(new TestEvent(3), new SimulationInstant(100));

        Assert.True(first.SequenceNumber < second.SequenceNumber);
        Assert.True(second.SequenceNumber < third.SequenceNumber);
    }

    [Fact]
    public void Publish_RecordsThePublishedAtInstant()
    {
        var bus = new SimulationEventBus();
        var instant = new SimulationInstant(500);

        PublishedEvent published = bus.Publish(new TestEvent(1), instant);

        Assert.Equal(instant, published.PublishedAt);
    }

    [Fact]
    public void TotalPublishedCount_TracksEveryPublishRegardlessOfType()
    {
        var bus = new SimulationEventBus();

        bus.Publish(new TestEvent(1), SimulationInstant.Epoch);
        bus.Publish(new OtherEvent("x"), SimulationInstant.Epoch);

        Assert.Equal(2, bus.TotalPublishedCount);
    }

    [Fact]
    public void Publish_WithNoSubscribers_DoesNotThrow()
    {
        var bus = new SimulationEventBus();
        PublishedEvent published = bus.Publish(new TestEvent(1), SimulationInstant.Epoch);

        Assert.NotNull(published);
    }
}
