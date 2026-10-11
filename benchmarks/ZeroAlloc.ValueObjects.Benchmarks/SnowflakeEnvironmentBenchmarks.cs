using BenchmarkDotNet.Attributes;
using ZeroAlloc.ValueObjects;

namespace ZeroAlloc.ValueObjects.Benchmarks;

// Snowflake New() with no registered provider, so the worker id comes from the
// ZA_SNOWFLAKE_WORKER_ID environment variable. TypedIdBenchmarks covers the provider path.
[MemoryDiagnoser]
public class SnowflakeEnvironmentBenchmarks
{
    private ISnowflakeWorkerIdProvider? _originalProvider;
    private string? _originalEnv;

    [GlobalSetup]
    public void Setup()
    {
        _originalProvider = TypedIdRuntime.SnowflakeProvider;
        _originalEnv = Environment.GetEnvironmentVariable("ZA_SNOWFLAKE_WORKER_ID");
        TypedIdRuntime.SnowflakeProvider = null;
        Environment.SetEnvironmentVariable("ZA_SNOWFLAKE_WORKER_ID", "1");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        TypedIdRuntime.SnowflakeProvider = _originalProvider;
        Environment.SetEnvironmentVariable("ZA_SNOWFLAKE_WORKER_ID", _originalEnv);
    }

    [Benchmark] public BenchSnowflakeId Snowflake_New_FromEnvironment() => BenchSnowflakeId.New();
}
