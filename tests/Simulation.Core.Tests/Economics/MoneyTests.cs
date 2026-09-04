using CapitalismSim.Simulation.Core.Economics;
using Xunit;

namespace CapitalismSim.Simulation.Core.Tests.Economics;

public sealed class MoneyTests
{
    [Fact]
    public void Constructor_StoresMinorUnitsAndCurrency()
    {
        var money = new Money(12345L, CurrencyCode.USD);

        Assert.Equal(12345L, money.MinorUnits);
        Assert.Equal(CurrencyCode.USD, money.Currency);
    }

    [Fact]
    public void Zero_CreatesZeroAmount()
    {
        var zero = Money.Zero(CurrencyCode.EUR);

        Assert.Equal(0L, zero.MinorUnits);
        Assert.Equal(CurrencyCode.EUR, zero.Currency);
        Assert.True(zero.IsZero);
    }

    [Fact]
    public void IsZero_TrueForZeroAmount()
    {
        var zero = Money.Zero(CurrencyCode.USD);
        var nonZero = new Money(1L, CurrencyCode.USD);

        Assert.True(zero.IsZero);
        Assert.False(nonZero.IsZero);
    }

    [Fact]
    public void Addition_SameCurrency_AddsMinorUnits()
    {
        var a = new Money(100L, CurrencyCode.USD);
        var b = new Money(50L, CurrencyCode.USD);

        var result = a + b;

        Assert.Equal(150L, result.MinorUnits);
        Assert.Equal(CurrencyCode.USD, result.Currency);
    }

    [Fact]
    public void Addition_DifferentCurrency_Throws()
    {
        var usd = new Money(100L, CurrencyCode.USD);
        var eur = new Money(100L, CurrencyCode.EUR);

        Assert.Throws<InvalidOperationException>(() => usd + eur);
    }

    [Fact]
    public void Subtraction_SameCurrency_SubtractsMinorUnits()
    {
        var a = new Money(100L, CurrencyCode.USD);
        var b = new Money(30L, CurrencyCode.USD);

        var result = a - b;

        Assert.Equal(70L, result.MinorUnits);
    }

    [Fact]
    public void Subtraction_DifferentCurrency_Throws()
    {
        var usd = new Money(100L, CurrencyCode.USD);
        var eur = new Money(50L, CurrencyCode.EUR);

        Assert.Throws<InvalidOperationException>(() => usd - eur);
    }

    [Fact]
    public void UnaryNegation_NegatesAmount()
    {
        var positive = new Money(100L, CurrencyCode.USD);

        var negative = -positive;

        Assert.Equal(-100L, negative.MinorUnits);
        Assert.Equal(CurrencyCode.USD, negative.Currency);
    }

    [Fact]
    public void Multiplication_ByInteger_MultipliesMinorUnits()
    {
        var money = new Money(100L, CurrencyCode.USD);

        var result = money * 3;

        Assert.Equal(300L, result.MinorUnits);
    }

    [Fact]
    public void Multiplication_Commutative()
    {
        var money = new Money(100L, CurrencyCode.USD);

        var left = money * 5;
        var right = 5 * money;

        Assert.Equal(left, right);
    }

    [Fact]
    public void Comparison_SameCurrency_ComparesMinorUnits()
    {
        var smaller = new Money(100L, CurrencyCode.USD);
        var larger = new Money(200L, CurrencyCode.USD);

        Assert.True(smaller < larger);
        Assert.True(smaller <= larger);
        Assert.True(larger > smaller);
        Assert.True(larger >= smaller);

        // Reflexivity: a value compares equal to itself
        var same = new Money(100L, CurrencyCode.USD);
        Assert.True(smaller <= same);
        Assert.True(smaller >= same);
    }

    [Fact]
    public void Comparison_DifferentCurrency_Throws()
    {
        var usd = new Money(100L, CurrencyCode.USD);
        var eur = new Money(200L, CurrencyCode.EUR);

        Assert.Throws<InvalidOperationException>(() => usd < eur);
        Assert.Throws<InvalidOperationException>(() => usd > eur);
    }

    [Fact]
    public void Equality_SameCurrencyAndAmount_AreEqual()
    {
        var a = new Money(100L, CurrencyCode.USD);
        var b = new Money(100L, CurrencyCode.USD);

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.False(a != b);
    }

    [Fact]
    public void Equality_DifferentAmountOrCurrency_AreNotEqual()
    {
        var a = new Money(100L, CurrencyCode.USD);
        var b = new Money(200L, CurrencyCode.USD);
        var c = new Money(100L, CurrencyCode.EUR);

        Assert.NotEqual(a, b);
        Assert.NotEqual(a, c);
        Assert.True(a != b);
        Assert.True(a != c);
    }

    [Fact]
    public void GetHashCode_ConsistentWithEquality()
    {
        var a = new Money(100L, CurrencyCode.USD);
        var b = new Money(100L, CurrencyCode.USD);

        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void CompareTo_SameCurrency_ComparesCorrectly()
    {
        var smaller = new Money(100L, CurrencyCode.USD);
        var larger = new Money(200L, CurrencyCode.USD);

        Assert.True(smaller.CompareTo(larger) < 0);
        Assert.True(larger.CompareTo(smaller) > 0);
        Assert.Equal(0, smaller.CompareTo(smaller));
    }

    [Fact]
    public void CompareTo_DifferentCurrency_Throws()
    {
        var usd = new Money(100L, CurrencyCode.USD);
        var eur = new Money(100L, CurrencyCode.EUR);

        Assert.Throws<InvalidOperationException>(() => usd.CompareTo(eur));
    }

    [Fact]
    public void ToString_ZeroAmount_ShowsSimpleFormat()
    {
        var zero = Money.Zero(CurrencyCode.USD);

        Assert.Equal("0 USD", zero.ToString());
    }

    [Fact]
    public void ToString_NonZeroAmount_ShowsMinorUnits()
    {
        var money = new Money(12345L, CurrencyCode.BRL);

        Assert.Equal("12345 BRL", money.ToString());
    }
}
