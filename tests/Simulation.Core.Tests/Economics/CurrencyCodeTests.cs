using CapitalismSim.Simulation.Core.Economics;
using Xunit;

namespace CapitalismSim.Simulation.Core.Tests.Economics;

public sealed class CurrencyCodeTests
{
    [Fact]
    public void Constructor_ValidThreeLetterCode_CreatesInstance()
    {
        var code = new CurrencyCode("USD");

        Assert.Equal("USD", code.Code);
    }

    [Theory]
    [InlineData("usd")]
    [InlineData("Usd")]
    [InlineData("uSd")]
    public void Constructor_LowerCaseOrMixedCase_Throws(string input)
    {
        Assert.Throws<ArgumentException>(() => new CurrencyCode(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("US")]
    [InlineData("USDD")]
    [InlineData("U")]
    public void Constructor_InvalidLength_Throws(string input)
    {
        Assert.Throws<ArgumentException>(() => new CurrencyCode(input));
    }

    [Theory]
    [InlineData("   ")]
    public void Constructor_Whitespace_Throws(string input)
    {
        Assert.Throws<ArgumentException>(() => new CurrencyCode(input));
    }

    [Fact]
    public void Constructor_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new CurrencyCode(null!));
    }

    [Fact]
    public void PredefinedInstances_AreValid()
    {
        Assert.Equal("USD", CurrencyCode.USD.Code);
        Assert.Equal("EUR", CurrencyCode.EUR.Code);
        Assert.Equal("BRL", CurrencyCode.BRL.Code);
    }

    [Fact]
    public void Equality_IsValueBased()
    {
        var a = new CurrencyCode("USD");
        var b = new CurrencyCode("USD");
        var c = new CurrencyCode("EUR");

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.NotEqual(a, c);
        Assert.True(a != c);
    }

    [Fact]
    public void GetHashCode_ConsistentWithEquality()
    {
        var a = new CurrencyCode("USD");
        var b = new CurrencyCode("USD");

        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void ToString_ReturnsCode()
    {
        var code = new CurrencyCode("BRL");

        Assert.Equal("BRL", code.ToString());
    }
}
