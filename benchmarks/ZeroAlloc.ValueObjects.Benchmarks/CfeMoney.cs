using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using CSharpFunctionalExtensions;

namespace ZeroAlloc.ValueObjects.Benchmarks;

// CFE baseline — boxing + iterator allocation per call
[SuppressMessage("Design", "MA0097:A class that implements IComparable<T> or IComparable should override comparison operators", Justification = "Benchmark baseline — comparison operators delegated to base class")]
public class CfeMoney : ValueObject
{
    public decimal Amount { get; }
    public string Currency { get; }
    public CfeMoney(decimal amount, string currency) => (Amount, Currency) = (amount, currency);
    protected override IEnumerable<IComparable> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }
}
