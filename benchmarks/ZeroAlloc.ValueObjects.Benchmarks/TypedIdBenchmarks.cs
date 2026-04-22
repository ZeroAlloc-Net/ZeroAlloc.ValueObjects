using BenchmarkDotNet.Attributes;
using ZeroAlloc.ValueObjects;

namespace ZeroAlloc.ValueObjects.Benchmarks;

// Anchor partials for the TypedId source generator. Multiple TypedId declarations co-locate
// here intentionally so the benchmark file stays self-contained.
// MA0048: co-locating several type declarations in one file is intentional.
#pragma warning disable MA0048

[TypedId(Strategy = IdStrategy.Ulid)]
public readonly partial record struct BenchUlidId;

[TypedId(Strategy = IdStrategy.Uuid7)]
public readonly partial record struct BenchUuid7Id;

[TypedId(Strategy = IdStrategy.Snowflake)]
public readonly partial record struct BenchSnowflakeId;

[TypedId(Strategy = IdStrategy.Sequential)]
public readonly partial record struct BenchSequentialId;

#pragma warning restore MA0048

[MemoryDiagnoser]
public class TypedIdBenchmarks
{
    private static readonly BenchUlidId SampleUlid = BenchUlidId.New();
    private static readonly string SampleUlidString = SampleUlid.ToString();

    private static readonly BenchUuid7Id SampleUuid7 = BenchUuid7Id.New();
    private static readonly string SampleUuid7String = SampleUuid7.ToString();

    private static readonly BenchSnowflakeId SampleSnowflake;
    private static readonly string SampleSnowflakeString;

    private static readonly BenchSequentialId SampleSeq;
    private static readonly string SampleSeqString;

    static TypedIdBenchmarks()
    {
        TypedIdRuntime.SnowflakeProvider = new StubProv(1);
        SampleSnowflake = BenchSnowflakeId.New();
        SampleSnowflakeString = SampleSnowflake.ToString();
        SampleSeq = BenchSequentialId.New();
        SampleSeqString = SampleSeq.ToString();
    }

    [Benchmark] public BenchUlidId Ulid_New() => BenchUlidId.New();
    [Benchmark] public string Ulid_ToString() => SampleUlid.ToString();
    [Benchmark] public BenchUlidId Ulid_Parse() => BenchUlidId.Parse(SampleUlidString);
    [Benchmark] public bool Ulid_TryParseSpan()
    {
        BenchUlidId.TryParse(SampleUlidString.AsSpan(), null, out var _);
        return true;
    }

    [Benchmark] public BenchUuid7Id Uuid7_New() => BenchUuid7Id.New();
    [Benchmark] public string Uuid7_ToString() => SampleUuid7.ToString();
    [Benchmark] public BenchUuid7Id Uuid7_Parse() => BenchUuid7Id.Parse(SampleUuid7String);

    [Benchmark] public BenchSnowflakeId Snowflake_New() => BenchSnowflakeId.New();
    [Benchmark] public string Snowflake_ToString() => SampleSnowflake.ToString();
    [Benchmark] public BenchSnowflakeId Snowflake_Parse() => BenchSnowflakeId.Parse(SampleSnowflakeString);

    [Benchmark] public BenchSequentialId Sequential_New() => BenchSequentialId.New();
    [Benchmark] public string Sequential_ToString() => SampleSeq.ToString();

    private sealed class StubProv : ISnowflakeWorkerIdProvider
    {
        public StubProv(int id) => WorkerId = id;
        public int WorkerId { get; }
    }
}
