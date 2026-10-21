using System;
using System.Buffers;
using ZeroAlloc.Serialisation;
using ZeroAlloc.TestHelpers;
using ZeroAlloc.ValueObjects;

namespace ZeroAlloc.ValueObjects.Tests;

// These TypedId structs are produced by the source generator at compile time; the
// partial declarations below supply nothing more than an anchor for the attribute.
// MA0048: co-locating several TypedId anchors in one test file is intentional.
#pragma warning disable MA0048

[TypedId(Strategy = IdStrategy.Ulid)]
public readonly partial record struct AllocUlidId;

[TypedId(Strategy = IdStrategy.Uuid7)]
public readonly partial record struct AllocUuid7Id;

[TypedId(Strategy = IdStrategy.Snowflake)]
public readonly partial record struct AllocSnowflakeId;

[TypedId(Strategy = IdStrategy.Sequential)]
public readonly partial record struct AllocSequentialId;

#pragma warning restore MA0048

// Gates for the rows docs/typed-id/benchmarks.md reports as 0 B: generation, parsing and the
// generated TypedIdSerializer. Snowflake generation shares the "SnowflakeStatics" collection
// with every other class that touches the provider slot or SnowflakeCore's state.
[Collection("SnowflakeStatics")]
public sealed class TypedIdAllocationTests : IDisposable
{
    private const int Iterations = 1000;

    private static readonly AllocUlidId Ulid = AllocUlidId.New();
    private static readonly string UlidText = Ulid.ToString();
    private static readonly AllocUuid7Id Uuid7 = AllocUuid7Id.New();
    private static readonly string Uuid7Text = Uuid7.ToString();
    private static readonly AllocSnowflakeId Snowflake = new(123_456_789_012_345_678L);
    private static readonly string SnowflakeText = Snowflake.ToString();
    private static readonly AllocSequentialId Sequential = new(1_234_567_890_123L);
    private static readonly string SequentialText = Sequential.ToString();

    private readonly ISnowflakeWorkerIdProvider? _originalProvider;

    public TypedIdAllocationTests()
    {
        _originalProvider = TypedIdRuntime.SnowflakeProvider;
        TypedIdRuntime.SnowflakeProvider = new StubProv(3);
    }

    public void Dispose() => TypedIdRuntime.SnowflakeProvider = _originalProvider;

    [Fact]
    public void Ulid_New_DoesNotAllocate() =>
        AllocationGate.AssertBudget(0, Iterations, () => AllocUlidId.New(), "Ulid New");

    [Fact]
    public void Uuid7_New_DoesNotAllocate() =>
        AllocationGate.AssertBudget(0, Iterations, () => AllocUuid7Id.New(), "Uuid7 New");

    [Fact]
    public void Snowflake_New_WithProvider_DoesNotAllocate() =>
        AllocationGate.AssertBudget(0, Iterations, () => AllocSnowflakeId.New(), "Snowflake New");

    [Fact]
    public void Sequential_New_DoesNotAllocate() =>
        AllocationGate.AssertBudget(0, Iterations, () => AllocSequentialId.New(), "Sequential New");

    [Fact]
    public void Parse_DoesNotAllocate_ForEveryKind()
    {
        AllocationGate.AssertBudget(0, Iterations, () => AllocUlidId.Parse(UlidText), "Ulid Parse");
        AllocationGate.AssertBudget(0, Iterations, () => AllocUuid7Id.Parse(Uuid7Text), "Uuid7 Parse");
        AllocationGate.AssertBudget(0, Iterations, () => AllocSnowflakeId.Parse(SnowflakeText), "Snowflake Parse");
        AllocationGate.AssertBudget(0, Iterations, () => AllocSequentialId.Parse(SequentialText), "Sequential Parse");
    }

    [Fact]
    public void TryParseSpan_DoesNotAllocate() =>
        AllocationGate.AssertBudget(
            0, Iterations, () => Assert.True(AllocUlidId.TryParse(UlidText.AsSpan(), null, out _)), "Ulid TryParse span");

    [Fact]
    public void Serializer_RoundTrip_DoesNotAllocate()
    {
        ISerializer<AllocUlidId> ulidSerializer = new AllocUlidId.TypedIdSerializer();
        ISerializer<AllocSnowflakeId> snowflakeSerializer = new AllocSnowflakeId.TypedIdSerializer();
        var buffer = new ArrayBufferWriter<byte>(256);

        ulidSerializer.Serialize(buffer, Ulid);
        var ulidBytes = buffer.WrittenSpan.ToArray();
        buffer.ResetWrittenCount();
        snowflakeSerializer.Serialize(buffer, Snowflake);
        var snowflakeBytes = buffer.WrittenSpan.ToArray();

        AllocationGate.AssertBudget(0, Iterations, () =>
        {
            buffer.ResetWrittenCount();
            ulidSerializer.Serialize(buffer, Ulid);
        }, "Ulid Serialize");
        AllocationGate.AssertBudget(0, Iterations, () => ulidSerializer.Deserialize(ulidBytes), "Ulid Deserialize");
        AllocationGate.AssertBudget(0, Iterations, () =>
        {
            buffer.ResetWrittenCount();
            snowflakeSerializer.Serialize(buffer, Snowflake);
        }, "Snowflake Serialize");
        AllocationGate.AssertBudget(
            0, Iterations, () => snowflakeSerializer.Deserialize(snowflakeBytes), "Snowflake Deserialize");
    }

    private sealed class StubProv : ISnowflakeWorkerIdProvider
    {
        public StubProv(int id) => WorkerId = id;
        public int WorkerId { get; }
    }
}
