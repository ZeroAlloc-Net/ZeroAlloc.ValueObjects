using ZeroAlloc.ValueObjects;

namespace ZeroAlloc.ValueObjects.Tests.FunctionalTests;

[ValueObject]
public partial class Product
{
    public string Name { get; }
    [IgnoreEqualityMember] public string InternalCode { get; }
    public Product(string name, string internalCode) =>
        (Name, InternalCode) = (name, internalCode);
}
