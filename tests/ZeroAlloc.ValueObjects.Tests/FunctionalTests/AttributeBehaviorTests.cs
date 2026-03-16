using ZeroAlloc.ValueObjects;

namespace ZeroAlloc.ValueObjects.Tests.FunctionalTests;

[ValueObject]
public partial class Address
{
    [EqualityMember] public string Street { get; }
    [EqualityMember] public string City { get; }
    public string Notes { get; }
    public Address(string street, string city, string notes) =>
        (Street, City, Notes) = (street, city, notes);
}

[ValueObject]
public partial class Product
{
    public string Name { get; }
    [IgnoreEqualityMember] public string InternalCode { get; }
    public Product(string name, string internalCode) =>
        (Name, InternalCode) = (name, internalCode);
}

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
