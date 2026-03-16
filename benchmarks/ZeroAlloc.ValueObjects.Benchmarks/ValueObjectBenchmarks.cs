using BenchmarkDotNet.Attributes;
using CSharpFunctionalExtensions;
using ZeroAlloc.ValueObjects;

namespace ZeroAlloc.ValueObjects.Benchmarks;

// CFE baseline — boxing + iterator allocation per call
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

// C# record — compiler-generated direct comparison, no allocation
public record RecordMoney(decimal Amount, string Currency);

// C# record struct — value-type record, stack allocated
public record struct RecordStructMoney(decimal Amount, string Currency);

// ZeroAlloc source-generated — direct comparison, no allocation
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
    private readonly RecordMoney _recA = new(10m, "USD");
    private readonly RecordMoney _recB = new(10m, "USD");
    private readonly RecordStructMoney _recStructA = new(10m, "USD");
    private readonly RecordStructMoney _recStructB = new(10m, "USD");
    private readonly ZaMoney _zaA = new(10m, "USD");
    private readonly ZaMoney _zaB = new(10m, "USD");

    [Benchmark(Baseline = true)]
    public bool CFE_Equals() => _cfeA.Equals(_cfeB);

    [Benchmark]
    public bool Record_Equals() => _recA.Equals(_recB);

    [Benchmark]
    public bool RecordStruct_Equals() => _recStructA.Equals(_recStructB);

    [Benchmark]
    public bool ZeroAlloc_Equals() => _zaA.Equals(_zaB);

    [Benchmark]
    public int CFE_GetHashCode() => _cfeA.GetHashCode();

    [Benchmark]
    public int Record_GetHashCode() => _recA.GetHashCode();

    [Benchmark]
    public int RecordStruct_GetHashCode() => _recStructA.GetHashCode();

    [Benchmark]
    public int ZeroAlloc_GetHashCode() => _zaA.GetHashCode();
}
