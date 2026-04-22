using System;
using ZeroAlloc.ValueObjects;

namespace ZeroAlloc.ValueObjects.Tests.GeneratorTests;

// These TypedId structs are produced by the source generator at compile time; the
// partial declarations below supply nothing more than an anchor for the attribute.
// MA0048: co-locating several TypedId anchors in one test file is intentional.
// MA0097: generated IComparable<T> does not provide comparison operators (by design:
// id types are sortable but not ordered for arithmetic semantics).
#pragma warning disable MA0048, MA0097

[TypedId(Strategy = IdStrategy.Ulid)]
public readonly partial record struct FunctionalOrderId;

[TypedId(Strategy = IdStrategy.Uuid7)]
public readonly partial record struct FunctionalMessageId;

[TypedId(Strategy = IdStrategy.Snowflake)]
public readonly partial record struct FunctionalSnowflakeId;

[TypedId(Strategy = IdStrategy.Sequential)]
public readonly partial record struct FunctionalSeqId;

#pragma warning restore MA0048, MA0097

public sealed class TypedIdFunctionalTests
{
    [Fact]
    public void Ulid_New_ProducesId_RoundTripsThroughString()
    {
        var id = FunctionalOrderId.New();
        var s = id.ToString();
        Assert.Equal(26, s.Length);
        Assert.True(FunctionalOrderId.TryParse(s, null, out var parsed));
        Assert.Equal(id, parsed);
    }

    [Fact]
    public void Uuid7_New_ProducesId_RoundTripsThroughString()
    {
        var id = FunctionalMessageId.New();
        var s = id.ToString();
        Assert.Equal(36, s.Length); // hyphenated UUID
        Assert.True(FunctionalMessageId.TryParse(s, null, out var parsed));
        Assert.Equal(id, parsed);
    }

    [Fact]
    public void Snowflake_New_WithConfiguredProvider_ProducesIncreasingIds()
    {
        var original = TypedIdRuntime.SnowflakeProvider;
        try
        {
            TypedIdRuntime.SnowflakeProvider = new TestProvider(42);
            var a = FunctionalSnowflakeId.New();
            var b = FunctionalSnowflakeId.New();
            Assert.True(b.Value > a.Value);

            var s = b.ToString();
            Assert.True(FunctionalSnowflakeId.TryParse(s, null, out var parsed));
            Assert.Equal(b, parsed);
        }
        finally
        {
            TypedIdRuntime.SnowflakeProvider = original;
        }
    }

    [Fact]
    public void Snowflake_New_WithoutProvider_ReadsFromEnvVar()
    {
        var original = TypedIdRuntime.SnowflakeProvider;
        var originalEnv = Environment.GetEnvironmentVariable("ZA_SNOWFLAKE_WORKER_ID");
        try
        {
            TypedIdRuntime.SnowflakeProvider = null;
            Environment.SetEnvironmentVariable("ZA_SNOWFLAKE_WORKER_ID", "5");

            var id = FunctionalSnowflakeId.New();
            Assert.True(id.Value > 0);
        }
        finally
        {
            TypedIdRuntime.SnowflakeProvider = original;
            Environment.SetEnvironmentVariable("ZA_SNOWFLAKE_WORKER_ID", originalEnv);
        }
    }

    [Fact]
    public void Snowflake_New_WithoutProviderOrEnvVar_Throws()
    {
        var original = TypedIdRuntime.SnowflakeProvider;
        var originalEnv = Environment.GetEnvironmentVariable("ZA_SNOWFLAKE_WORKER_ID");
        try
        {
            TypedIdRuntime.SnowflakeProvider = null;
            Environment.SetEnvironmentVariable("ZA_SNOWFLAKE_WORKER_ID", null);

            Assert.Throws<TypedIdException>(() => FunctionalSnowflakeId.New());
        }
        finally
        {
            TypedIdRuntime.SnowflakeProvider = original;
            Environment.SetEnvironmentVariable("ZA_SNOWFLAKE_WORKER_ID", originalEnv);
        }
    }

    [Fact]
    public void Sequential_New_Increases()
    {
        SequentialCore.Reset();
        var a = FunctionalSeqId.New();
        var b = FunctionalSeqId.New();
        Assert.True(b.Value > a.Value);

        var s = b.ToString();
        Assert.True(FunctionalSeqId.TryParse(s, null, out var parsed));
        Assert.Equal(b, parsed);
    }

    [Fact]
    public void AllStrategies_ImplementExpectedInterfaces()
    {
        Assert.IsAssignableFrom<IEquatable<FunctionalOrderId>>(FunctionalOrderId.New());
        Assert.IsAssignableFrom<IComparable<FunctionalOrderId>>(FunctionalOrderId.New());

        // IParsable<T> / ISpanParsable<T> are static-abstract interfaces (net7+). Verify
        // the generated struct satisfies the constraint at compile time.
        AssertSatisfiesParsable<FunctionalOrderId>();
        AssertSatisfiesParsable<FunctionalMessageId>();
        AssertSatisfiesParsable<FunctionalSnowflakeId>();
        AssertSatisfiesParsable<FunctionalSeqId>();
    }

    private static void AssertSatisfiesParsable<T>() where T : IParsable<T>, ISpanParsable<T>
    {
        // Compile-time constraint check only; nothing to assert at runtime.
    }

    private sealed class TestProvider : ISnowflakeWorkerIdProvider
    {
        public TestProvider(int id) => WorkerId = id;
        public int WorkerId { get; }
    }
}
