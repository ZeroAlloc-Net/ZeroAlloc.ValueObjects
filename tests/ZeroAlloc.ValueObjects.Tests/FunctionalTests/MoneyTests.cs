using ZeroAlloc.ValueObjects;

namespace ZeroAlloc.ValueObjects.Tests.FunctionalTests;

[ValueObject]
public partial class Money
{
    public decimal Amount { get; }
    public string Currency { get; }
    public Money(decimal amount, string currency) => (Amount, Currency) = (amount, currency);
}

public class MoneyTests
{
    [Fact]
    public void Equals_ReturnTrue_WhenPropertiesMatch()
    {
        var a = new Money(10m, "USD");
        var b = new Money(10m, "USD");
        Assert.Equal(a, b);
    }

    [Fact]
    public void Equals_ReturnsFalse_WhenPropertiesDiffer()
    {
        var a = new Money(10m, "USD");
        var b = new Money(20m, "USD");
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void GetHashCode_ReturnsSameValue_ForEqualObjects()
    {
        var a = new Money(10m, "USD");
        var b = new Money(10m, "USD");
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void CanBeUsedAsDictionaryKey()
    {
        var dict = new Dictionary<Money, string>();
        var key = new Money(10m, "USD");
        dict[key] = "ten dollars";
        Assert.Equal("ten dollars", dict[new Money(10m, "USD")]);
    }

    [Fact]
    public void CanBeUsedInHashSet()
    {
        var set = new HashSet<Money> { new(10m, "USD"), new(10m, "USD"), new(20m, "EUR") };
        Assert.Equal(2, set.Count);
    }

    [Fact]
    public void OperatorEquals_Works()
    {
        Assert.True(new Money(10m, "USD") == new Money(10m, "USD"));
        Assert.False(new Money(10m, "USD") == new Money(10m, "EUR"));
    }

    [Fact]
    public void ToString_ContainsPropertyValues()
    {
        var money = new Money(10m, "USD");
        Assert.Contains("10", money.ToString());
        Assert.Contains("USD", money.ToString());
    }

    [Fact]
    public void ObjectEquals_ReturnsFalse_ForNull()
    {
        var money = new Money(10m, "USD");
        Assert.False(money.Equals((object?)null));
    }

    [Fact]
    public void ObjectEquals_ReturnsFalse_ForDifferentType()
    {
        var money = new Money(10m, "USD");
        Assert.False(money.Equals("10 USD"));
    }

    [Fact]
    public void OperatorNotEquals_Works()
    {
        Assert.True(new Money(10m, "USD") != new Money(20m, "USD"));
        Assert.False(new Money(10m, "USD") != new Money(10m, "USD"));
    }
}
