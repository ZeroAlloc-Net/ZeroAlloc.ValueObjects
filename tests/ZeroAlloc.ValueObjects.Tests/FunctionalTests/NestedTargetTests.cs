using System.Text.Json;

namespace ZeroAlloc.ValueObjects.Tests.FunctionalTests;

/// <summary>
/// Nested and generic value objects, and nested typed IDs, built by the real generator in this
/// project, behave like top-level ones (#196). Before the fix their members went to new top-level
/// types, and this file did not compile.
/// </summary>
public sealed partial class NestedTargetTests
{
    [ValueObject]
    public partial class Money
    {
        public Money(decimal amount, string currency) => (Amount, Currency) = (amount, currency);
        public decimal Amount { get; }
        public string Currency { get; }
    }

    [ValueObject]
    public readonly partial struct Range<T>
    {
        public Range(T low, T high) => (Low, High) = (low, high);
        public T Low { get; }
        public T High { get; }
    }

    [ValueObject]
    public partial class Tagged<T>
    {
        public Tagged(T? tag) => Tag = tag;
        public T? Tag { get; }
    }

    [TypedId(Strategy = IdStrategy.Uuid7)]
    public readonly partial record struct OrderId;

    [TypedId(Strategy = IdStrategy.Sequential)]
    internal readonly partial record struct LineId;

    [Fact]
    public void NestedValueObject_ComparesByItsMembers()
    {
        var a = new Money(10m, "EUR");
        var b = new Money(10m, "EUR");

        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, new Money(10m, "USD"));
        Assert.Equal("Money { Amount = 10, Currency = EUR }", a.ToString());
    }

    [Fact]
    public void GenericValueObject_ComparesMembersOfItsTypeParameter()
    {
        Assert.True(new Range<int>(1, 2) == new Range<int>(1, 2));
        Assert.True(new Range<string>("a", "b") != new Range<string>("a", "c"));
        Assert.Equal(new Range<string>("a", "b").GetHashCode(), new Range<string>("a", "b").GetHashCode());

        Assert.True(new Tagged<string>(null) == new Tagged<string>(null));
        Assert.False(new Tagged<string>(null) == new Tagged<string>("x"));
        Assert.True(new Tagged<int?>(3) == new Tagged<int?>(3));
        Assert.Equal(0, new Tagged<string>(null).GetHashCode());
        Assert.Equal(string.Empty, new Tagged<string>(null).ToString());
        Assert.Equal("x", new Tagged<string>("x").ToString());
    }

    [Fact]
    public void NestedTypedId_RoundTripsThroughStringAndJson()
    {
        var id = OrderId.New();

        Assert.Equal(id, OrderId.Parse(id.ToString()));
        var json = JsonSerializer.Serialize(id);
        Assert.Equal($"\"{id}\"", json);
        Assert.Equal(id, JsonSerializer.Deserialize<OrderId>(json));
    }

    [Fact]
    public void NestedInternalTypedId_IsGenerated()
    {
        var first = LineId.New();
        var second = LineId.New();

        Assert.True(second > first);
        Assert.Equal(second, LineId.Parse(second.ToString()));
    }
}
