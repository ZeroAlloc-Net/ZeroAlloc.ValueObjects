using ZeroAlloc.ValueObjects;

namespace ZeroAlloc.ValueObjects.Tests.FunctionalTests;

// The multi-property struct counterpart of Money, as in the README's performance table.
[ValueObject]
public readonly partial struct MoneyStruct
{
    public decimal Amount { get; }
    public string Currency { get; }
    public MoneyStruct(decimal amount, string currency) => (Amount, Currency) = (amount, currency);
}
