using CapitalismSim.Simulation.Core.Commands;
using Xunit;

namespace CapitalismSim.Simulation.Core.Tests.Commands;

public sealed class CommandDispatcherTests
{
    private sealed record TestCommand(int Value) : ISimulationCommand;

    private sealed class TestCommandHandler : ISimulationCommandHandler<TestCommand>
    {
        public List<int> ReceivedValues { get; } = new();

        public void Handle(TestCommand command) => ReceivedValues.Add(command.Value);
    }

    private static readonly int[] s_expected42 = [42];
    private static readonly int[] s_expected123 = [1, 2, 3];

    [Fact]
    public void Dispatch_InvokesRegisteredHandler()
    {
        var dispatcher = new CommandDispatcher();
        var handler = new TestCommandHandler();
        dispatcher.RegisterHandler<TestCommand>(handler);

        dispatcher.Dispatch(new TestCommand(42));

        Assert.Equal(s_expected42, handler.ReceivedValues);
    }

    [Fact]
    public void Dispatch_WithoutHandler_Throws()
    {
        var dispatcher = new CommandDispatcher();

        Assert.Throws<InvalidOperationException>(() => dispatcher.Dispatch(new TestCommand(1)));
    }

    [Fact]
    public void RegisterHandler_SecondHandlerForSameType_Throws()
    {
        var dispatcher = new CommandDispatcher();
        dispatcher.RegisterHandler<TestCommand>(new TestCommandHandler());

        Assert.Throws<InvalidOperationException>(() => dispatcher.RegisterHandler<TestCommand>(new TestCommandHandler()));
    }

    [Fact]
    public void HasHandler_ReflectsRegistrationState()
    {
        var dispatcher = new CommandDispatcher();
        Assert.False(dispatcher.HasHandler<TestCommand>());

        dispatcher.RegisterHandler<TestCommand>(new TestCommandHandler());

        Assert.True(dispatcher.HasHandler<TestCommand>());
    }

    [Fact]
    public void Dispatch_PreservesOrderAcrossMultipleCalls()
    {
        var dispatcher = new CommandDispatcher();
        var handler = new TestCommandHandler();
        dispatcher.RegisterHandler<TestCommand>(handler);

        dispatcher.Dispatch(new TestCommand(1));
        dispatcher.Dispatch(new TestCommand(2));
        dispatcher.Dispatch(new TestCommand(3));

        Assert.Equal(s_expected123, handler.ReceivedValues);
    }
}
