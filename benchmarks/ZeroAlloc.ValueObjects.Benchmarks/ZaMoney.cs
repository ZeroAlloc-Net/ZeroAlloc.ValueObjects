using ZeroAlloc.ValueObjects;

namespace ZeroAlloc.ValueObjects.Benchmarks;

// ZeroAlloc source-generated class — direct comparison, no allocation
[ValueObject]
public partial class ZaMoney
{
    public decimal Amount { get; }
    public string Currency { get; }
    public ZaMoney(decimal amount, string currency) => (Amount, Currency) = (amount, currency);
}
