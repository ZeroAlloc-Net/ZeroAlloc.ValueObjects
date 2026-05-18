using System.Globalization;
using System.Threading;
using ZeroAlloc.ValueObjects;

#pragma warning disable MA0048 // File name must match type name — fixture types co-located with their tests.

namespace ZeroAlloc.ValueObjects.Tests.FunctionalTests;

[ValueObject]
public readonly partial struct IntVo
{
    public int Value { get; }
    public IntVo(int value) => Value = value;
}

[ValueObject]
public readonly partial struct StringVo
{
    public string Value { get; }
    public StringVo(string value) => Value = value;
}

[ValueObject]
public readonly partial struct NullableStringVo
{
    public string? Value { get; }
    public NullableStringVo(string? value) => Value = value;
}

[ValueObject]
public readonly partial struct DateTimeVo
{
    public System.DateTime Value { get; }
    public DateTimeVo(System.DateTime value) => Value = value;
}

[ValueObject]
public partial class GreetingVo
{
    public string Name { get; init; } = "";
    public int Times { get; init; }
}

public class SinglePropertyEmitTests
{
    [Fact]
    public void IntValueObject_ToString_ReturnsInvariantCultureDigits()
    {
        var id = new IntVo(42);
        Assert.Equal("42", id.ToString());
    }

    [Fact]
    public void StringValueObject_ToString_ReturnsRawString()
    {
        var vo = new StringVo("hello");
        Assert.Equal("hello", vo.ToString());
    }

    [Fact]
    public void NullableStringValueObject_ToString_NullReturnsEmpty()
    {
        var vo = new NullableStringVo(null);
        Assert.Equal("", vo.ToString());
    }

    [Fact]
    public void DateTimeValueObject_ToString_UsesInvariantCulture()
    {
        var de = CultureInfo.GetCultureInfo("de-DE");
        var prior = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = de;
            var when = new System.DateTime(2026, 5, 18, 10, 0, 0, System.DateTimeKind.Utc);
            var vo = new DateTimeVo(when);
            // The generator emits Value.ToString(CultureInfo.InvariantCulture) — assert that's
            // independent of the thread culture set above.
            Assert.Equal(when.ToString(CultureInfo.InvariantCulture), vo.ToString());
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = prior;
        }
    }

    [Fact]
    public void IntValueObject_GetHashCode_EqualsValueGetHashCode()
    {
        var id = new IntVo(42);
        Assert.Equal(42.GetHashCode(), id.GetHashCode());
    }

    [Fact]
    public void NullableStringValueObject_GetHashCode_NullReturnsZero()
    {
        var vo = new NullableStringVo(null);
        Assert.Equal(0, vo.GetHashCode());
    }

    [Fact]
    public void MultiProperty_ToString_KeepsWrappedFormat()
    {
        var vo = new GreetingVo { Name = "Marcel", Times = 3 };
        // Regression guard: multi-property [ValueObject] still emits the record-style wrapped form.
        Assert.Equal("GreetingVo { Name = Marcel, Times = 3 }", vo.ToString());
    }
}
