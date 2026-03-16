using BenchmarkDotNet.Attributes;

namespace ZeroAlloc.ValueObjects.Benchmarks;

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
    private readonly ZaMoneyStruct _zaStructA = new(10m, "USD");
    private readonly ZaMoneyStruct _zaStructB = new(10m, "USD");

    [Benchmark(Baseline = true)]
    public bool CFE_Equals() => _cfeA.Equals(_cfeB);

    [Benchmark]
    public bool Record_Equals() => _recA.Equals(_recB);

    [Benchmark]
    public bool RecordStruct_Equals() => _recStructA.Equals(_recStructB);

    [Benchmark]
    public bool ZeroAlloc_Equals() => _zaA.Equals(_zaB);

    [Benchmark]
    public bool ZeroAllocStruct_Equals() => _zaStructA.Equals(_zaStructB);

    [Benchmark]
    public int CFE_GetHashCode() => _cfeA.GetHashCode();

    [Benchmark]
    public int Record_GetHashCode() => _recA.GetHashCode();

    [Benchmark]
    public int RecordStruct_GetHashCode() => _recStructA.GetHashCode();

    [Benchmark]
    public int ZeroAlloc_GetHashCode() => _zaA.GetHashCode();

    [Benchmark]
    public int ZeroAllocStruct_GetHashCode() => _zaStructA.GetHashCode();
}
