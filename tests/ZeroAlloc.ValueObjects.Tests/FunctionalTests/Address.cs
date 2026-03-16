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
