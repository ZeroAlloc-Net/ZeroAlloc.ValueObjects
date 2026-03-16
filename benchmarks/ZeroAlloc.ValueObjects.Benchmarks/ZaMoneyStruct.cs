using ZeroAlloc.ValueObjects;

namespace ZeroAlloc.ValueObjects.Benchmarks;

// ZeroAlloc source-generated struct — value type, direct comparison, no allocation
[ValueObject]
public readonly partial struct ZaMoneyStruct
{
    public decimal Amount { get; }
    public string Currency { get; }
    public ZaMoneyStruct(decimal amount, string currency) => (Amount, Currency) = (amount, currency);
}
