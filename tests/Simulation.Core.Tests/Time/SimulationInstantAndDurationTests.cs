using CapitalismSim.Simulation.Core.Time;
using Xunit;

namespace CapitalismSim.Simulation.Core.Tests.Time;

public sealed class SimulationInstantAndDurationTests
{
    [Fact]
    public void Comparison_OrdersInstantsBySeconds()
    {
        var earlier = new SimulationInstant(100);
        var later = new SimulationInstant(200);

        Assert.True(earlier < later);
        Assert.True(later > earlier);
        Assert.True(earlier <= later);
        Assert.True(later >= earlier);
        Assert.Equal(-1, earlier.CompareTo(later));
    }

    [Fact]
    public void Add_MovesInstantForwardByDuration()
    {
        var start = new SimulationInstant(1000);
        var duration = SimulationDuration.FromSeconds(500);

        SimulationInstant result = start.Add(duration);

        Assert.Equal(1500, result.TotalSeconds);
    }

    [Fact]
    public void Since_ComputesDurationBetweenTwoInstants()
    {
        var earlier = new SimulationInstant(1000);
        var later = new SimulationInstant(1500);

        SimulationDuration duration = later.Since(earlier);

        Assert.Equal(500, duration.TotalSeconds);
    }

    [Fact]
    public void OperatorMinus_BetweenInstants_ComputesDuration()
    {
        var earlier = new SimulationInstant(10);
        var later = new SimulationInstant(30);

        SimulationDuration duration = later - earlier;

        Assert.Equal(20, duration.TotalSeconds);
    }

    [Fact]
    public void DurationFactories_ConvertUnitsCorrectly()
    {
        Assert.Equal(60, SimulationDuration.FromMinutes(1).TotalSeconds);
        Assert.Equal(3600, SimulationDuration.FromHours(1).TotalSeconds);
        Assert.Equal(86_400, SimulationDuration.FromDays(1).TotalSeconds);
    }

    [Fact]
    public void NegativeDuration_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SimulationDuration(-1));
    }

    [Fact]
    public void Add_Overflow_ThrowsOverflowException()
    {
        var nearMax = new SimulationInstant(long.MaxValue);
        var duration = SimulationDuration.FromSeconds(1);

        Assert.Throws<OverflowException>(() => nearMax.Add(duration));
    }

    [Fact]
    public void Equality_IsValueBased()
    {
        var a = new SimulationInstant(42);
        var b = new SimulationInstant(42);

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.False(a != b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }
}
