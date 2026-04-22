namespace ZeroAlloc.ValueObjects.Tests;

public class TypedIdAttributeTests
{
    [Fact]
    public void TypedIdAttribute_AllowsInitOnlyStrategyAndBacking()
    {
        var attr = new TypedIdAttribute { Strategy = IdStrategy.Ulid, Backing = BackingType.Guid };
        Assert.Equal(IdStrategy.Ulid, attr.Strategy);
        Assert.Equal(BackingType.Guid, attr.Backing);
    }

    [Fact]
    public void TypedIdDefaultAttribute_DefaultsToUlidAndAuto()
    {
        var attr = new TypedIdDefaultAttribute();
        Assert.Equal(IdStrategy.Ulid, attr.Strategy);
        Assert.Equal(BackingType.Default, attr.Backing);
    }

    [Fact]
    public void BackingType_Default_IsZero() => Assert.Equal(0, (int)BackingType.Default);
}
