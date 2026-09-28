using System;
using System.Globalization;
using System.Reflection;

namespace ZeroAlloc.ValueObjects.Tests;

// The generated string Parse must not carry an optional provider parameter. The span overload
// next to it has the same parameter count, so an optional one trips RS0027 in every consumer that
// tracks its public API. The one-argument form is a separate overload instead. See issue #191.
public sealed class TypedIdParseOverloadTests
{
    public static TheoryData<Type> TypedIds => new()
    {
        typeof(JsonUlidId),
        typeof(JsonUuid7Id),
        typeof(JsonSnowflakeId),
        typeof(JsonSequentialId),
    };

    [Theory]
    [MemberData(nameof(TypedIds))]
    public void StringParseWithProvider_HasNoOptionalParameter(Type id)
    {
        var parse = id.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, [typeof(string), typeof(IFormatProvider)]);

        Assert.NotNull(parse);
        Assert.All(parse.GetParameters(), p => Assert.False(p.IsOptional, $"{id.Name}.Parse parameter '{p.Name}' is optional"));
    }

    [Theory]
    [MemberData(nameof(TypedIds))]
    public void StringParseWithoutProvider_IsItsOwnOverload(Type id)
    {
        var parse = id.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, [typeof(string)]);

        Assert.NotNull(parse);
        Assert.Equal(id, parse.ReturnType);
    }

    [Fact]
    public void BothStringOverloads_ParseTheSameValue()
    {
        var original = JsonSequentialId.New();
        var text = original.ToString();

        Assert.Equal(original, JsonSequentialId.Parse(text));
        Assert.Equal(original, JsonSequentialId.Parse(text, CultureInfo.InvariantCulture));
    }
}
