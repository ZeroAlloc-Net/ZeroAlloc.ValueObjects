using ZeroAlloc.ValueObjects;

namespace ZeroAlloc.ValueObjects.Tests.FunctionalTests;

[ValueObject]
public partial struct CustomerId
{
    public int Value { get; }
    public CustomerId(int value) => Value = value;
}

public class StructTests
{
    [Fact]
    public void Equals_ReturnsTrue_WhenValuesMatch()
    {
        var a = new CustomerId(42);
        var b = new CustomerId(42);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Equals_ReturnsFalse_WhenValuesDiffer()
    {
        var a = new CustomerId(1);
        var b = new CustomerId(2);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void GetHashCode_IsSame_ForEqualStructs()
    {
        Assert.Equal(new CustomerId(42).GetHashCode(), new CustomerId(42).GetHashCode());
    }

    [Fact]
    public void CanBeUsedInHashSet()
    {
        var set = new HashSet<CustomerId> { new(1), new(1), new(2) };
        Assert.Equal(2, set.Count);
    }

    [Fact]
    public void OperatorEquals_Works()
    {
        Assert.True(new CustomerId(1) == new CustomerId(1));
        Assert.False(new CustomerId(1) == new CustomerId(2));
    }

    [Fact]
    public void OperatorNotEquals_Works()
    {
        Assert.True(new CustomerId(1) != new CustomerId(2));
        Assert.False(new CustomerId(1) != new CustomerId(1));
    }

    [Fact]
    public void ObjectEquals_ReturnsFalse_ForBoxedDifferentType()
    {
        var id = new CustomerId(1);
        Assert.False(id.Equals((object)"1"));
    }
}
