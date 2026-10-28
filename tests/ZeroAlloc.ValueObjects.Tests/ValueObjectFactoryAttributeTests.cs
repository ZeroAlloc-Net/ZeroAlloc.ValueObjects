using System;
using System.Reflection;

namespace ZeroAlloc.ValueObjects.Tests;

public class ValueObjectFactoryAttributeTests
{
    [ValueObjectFactory(nameof(TryCreate))]
    private sealed class Sample
    {
        internal static bool TryCreate(string? input, out Sample? value)
        {
            value = input is null ? null : new Sample();
            return value is not null;
        }
    }

    [Fact]
    public void Constructor_StoresMethodName() =>
        Assert.Equal("Build", new ValueObjectFactoryAttribute("Build").MethodName);

    [Fact]
    public void Attribute_IsReadableFromAType()
    {
        var attr = typeof(Sample).GetCustomAttribute<ValueObjectFactoryAttribute>();
        Assert.NotNull(attr);
        Assert.Equal("TryCreate", attr!.MethodName);
    }

    [Fact]
    public void Attribute_TargetsClassesAndStructsOnly()
    {
        var usage = typeof(ValueObjectFactoryAttribute).GetCustomAttribute<AttributeUsageAttribute>()!;
        Assert.Equal(AttributeTargets.Class | AttributeTargets.Struct, usage.ValidOn);
        Assert.False(usage.Inherited);
    }
}
