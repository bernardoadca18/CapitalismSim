using CapitalismSim.Simulation.Core.Time;
using Xunit;

namespace CapitalismSim.Simulation.Core.Tests.Time;

public sealed class SimulationClockTests
{
    [Fact]
    public void Advance_MovesClockForwardByDuration()
    {
        var clock = new SimulationClock(SimulationInstant.Epoch);

        clock.Advance(SimulationDuration.FromMinutes(5));

        Assert.Equal(300, clock.Current.TotalSeconds);
    }

    [Fact]
    public void AdvanceTo_SetsClockToExactInstant()
    {
        var clock = new SimulationClock(SimulationInstant.Epoch);

        clock.AdvanceTo(new SimulationInstant(12345));

        Assert.Equal(12345, clock.Current.TotalSeconds);
    }

    [Fact]
    public void AdvanceTo_Backwards_Throws()
    {
        var clock = new SimulationClock(new SimulationInstant(1000));

        Assert.Throws<ArgumentException>(() => clock.AdvanceTo(new SimulationInstant(500)));
    }

    [Fact]
    public void Restore_CanMoveClockBackwardsWithoutThrowing()
    {
        var clock = new SimulationClock(new SimulationInstant(1000));

        clock.Restore(new SimulationInstant(0));

        Assert.Equal(0, clock.Current.TotalSeconds);
    }
}
