namespace ZeroAlloc.ValueObjects.Tests.FunctionalTests;

public class AttributeBehaviorTests
{
    [Fact]
    public void EqualityMember_IncludesOnlyMarkedProps_Equal()
    {
        var a = new Address("1 Main St", "Springfield", "notes A");
        var b = new Address("1 Main St", "Springfield", "notes B");
        Assert.Equal(a, b);
    }

    [Fact]
    public void EqualityMember_IncludesOnlyMarkedProps_NotEqual()
    {
        var a = new Address("1 Main St", "Springfield", "same notes");
        var b = new Address("2 Elm St", "Springfield", "same notes");
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void EqualityMember_HashCode_IgnoresUnmarkedProp()
    {
        var a = new Address("1 Main St", "Springfield", "notes A");
        var b = new Address("1 Main St", "Springfield", "notes B");
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void IgnoreEqualityMember_ExcludesMarkedProp_Equal()
    {
        var a = new Product("Widget", "CODE-001");
        var b = new Product("Widget", "CODE-999");
        Assert.Equal(a, b);
    }

    [Fact]
    public void IgnoreEqualityMember_ExcludesMarkedProp_NotEqual()
    {
        var a = new Product("Widget", "CODE-001");
        var b = new Product("Gadget", "CODE-001");
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void IgnoreEqualityMember_HashCode_IgnoresExcludedProp()
    {
        var a = new Product("Widget", "CODE-001");
        var b = new Product("Widget", "CODE-999");
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }
}
