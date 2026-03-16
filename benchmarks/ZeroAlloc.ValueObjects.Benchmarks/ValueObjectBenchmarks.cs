using BenchmarkDotNet.Attributes;
using CSharpFunctionalExtensions;
using ZeroAlloc.ValueObjects;

namespace ZeroAlloc.ValueObjects.Benchmarks;

// CFE baseline
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

// ZeroAlloc generated
[ZeroAlloc.ValueObjects.ValueObject]
public partial class ZaMoney
{
    public decimal Amount { get; }
    public string Currency { get; }
    public ZaMoney(decimal amount, string currency) => (Amount, Currency) = (amount, currency);
}

[MemoryDiagnoser]
public class ValueObjectBenchmarks
{
    private readonly CfeMoney _cfeA = new(10m, "USD");
    private readonly CfeMoney _cfeB = new(10m, "USD");
    private readonly ZaMoney _zaA = new(10m, "USD");
    private readonly ZaMoney _zaB = new(10m, "USD");

    [Benchmark(Baseline = true)]
    public bool CFE_Equals() => _cfeA.Equals(_cfeB);

    [Benchmark]
    public bool ZeroAlloc_Equals() => _zaA.Equals(_zaB);

    [Benchmark]
    public int CFE_GetHashCode() => _cfeA.GetHashCode();

    [Benchmark]
    public int ZeroAlloc_GetHashCode() => _zaA.GetHashCode();
}
