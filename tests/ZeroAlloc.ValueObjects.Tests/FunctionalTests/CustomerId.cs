using ZeroAlloc.ValueObjects;

namespace ZeroAlloc.ValueObjects.Tests.FunctionalTests;

[ValueObject]
public partial struct CustomerId
{
    public int Value { get; }
    public CustomerId(int value) => Value = value;
}
