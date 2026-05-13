using BenchmarkDotNet.Attributes;
using Vogen;
using ZeroAlloc.ValueObjects;

namespace ZeroAlloc.ValueObjects.Benchmarks;

// Vogen is the other source-generator value-object library in .NET. It wraps
// a single primitive type with strong-typing, equality, and parsing — the
// same sweet spot as ZA.ValueObjects' TypedId.
//
// The existing ValueObjectBenchmarks compares ZA's two-field Money against
// CSharpFunctionalExtensions / record / record struct. Vogen does not support
// multi-field types natively, so this file adds the apples-to-apples
// comparison Vogen *is* designed for: a single-int wrapped strongly-typed ID.

#pragma warning disable MA0048

[ValueObject<int>]
public partial struct VogenIntId { }

// ZA.ValueObjects' apples-to-apples wrapper: single-int value object via the
// [ValueObject] attribute (same generative shape as Vogen — From-style factory,
// equality/hash/ToString emitted at compile time, no allocation).
[ValueObject]
public readonly partial struct ZaIntId
{
    public int Value { get; }
    public ZaIntId(int value) => Value = value;
    public static ZaIntId From(int value) => new(value);
}

#pragma warning restore MA0048

[MemoryDiagnoser]
public class VogenComparisonBenchmark
{
    private readonly VogenIntId _vogenA = VogenIntId.From(42);
    private readonly VogenIntId _vogenB = VogenIntId.From(42);
    private readonly VogenIntId _vogenC = VogenIntId.From(43);

    private readonly ZaIntId _zaA = ZaIntId.From(42);
    private readonly ZaIntId _zaB = ZaIntId.From(42);
    private readonly ZaIntId _zaC = ZaIntId.From(43);

    // --- Construction ---

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Construct")]
    public VogenIntId Vogen_From() => VogenIntId.From(123);

    [Benchmark]
    [BenchmarkCategory("Construct")]
    public ZaIntId Za_From() => ZaIntId.From(123);

    // --- Equality (equal) ---

    [Benchmark]
    [BenchmarkCategory("Equals_Equal")]
    public bool Vogen_Equals_Equal() => _vogenA.Equals(_vogenB);

    [Benchmark]
    [BenchmarkCategory("Equals_Equal")]
    public bool Za_Equals_Equal() => _zaA.Equals(_zaB);

    // --- Equality (not equal) ---

    [Benchmark]
    [BenchmarkCategory("Equals_NotEqual")]
    public bool Vogen_Equals_NotEqual() => _vogenA.Equals(_vogenC);

    [Benchmark]
    [BenchmarkCategory("Equals_NotEqual")]
    public bool Za_Equals_NotEqual() => _zaA.Equals(_zaC);

    // --- GetHashCode ---

    [Benchmark]
    [BenchmarkCategory("GetHashCode")]
    public int Vogen_GetHashCode() => _vogenA.GetHashCode();

    [Benchmark]
    [BenchmarkCategory("GetHashCode")]
    public int Za_GetHashCode() => _zaA.GetHashCode();

    // --- ToString ---

    [Benchmark]
    [BenchmarkCategory("ToString")]
    public string Vogen_ToString() => _vogenA.ToString();

    [Benchmark]
    [BenchmarkCategory("ToString")]
    public string Za_ToString() => _zaA.ToString();
}
