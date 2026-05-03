using System;
using System.Buffers;
using ZeroAlloc.Serialisation;
using ZeroAlloc.ValueObjects;

namespace ZeroAlloc.ValueObjects.Tests;

// These TypedId structs are produced by the source generator at compile time; the
// partial declarations below supply nothing more than an anchor for the attribute.
// MA0048: co-locating several TypedId anchors in one test file is intentional.
#pragma warning disable MA0048

[TypedId(Strategy = IdStrategy.Ulid)]
public readonly partial record struct SerUlidId;

[TypedId(Strategy = IdStrategy.Uuid7)]
public readonly partial record struct SerUuid7Id;

[TypedId(Strategy = IdStrategy.Snowflake)]
public readonly partial record struct SerSnowflakeId;

[TypedId(Strategy = IdStrategy.Sequential)]
public readonly partial record struct SerSequentialId;

#pragma warning restore MA0048

// Shares a collection with other tests that mutate TypedIdRuntime.SnowflakeProvider to
// avoid parallel-class races on the static provider slot.
[Collection("SnowflakeProviderMutation")]
public sealed class TypedIdSerializerTests : IDisposable
{
    private readonly ISnowflakeWorkerIdProvider? _originalProvider;

    public TypedIdSerializerTests()
    {
        _originalProvider = TypedIdRuntime.SnowflakeProvider;
        TypedIdRuntime.SnowflakeProvider = new StubProv(11);
    }

    public void Dispose() => TypedIdRuntime.SnowflakeProvider = _originalProvider;

    [Fact]
    public void Ulid_RoundTrips_ThroughISerializer()
    {
        ISerializer<SerUlidId> serializer = new SerUlidId.TypedIdSerializer();
        var original = SerUlidId.New();

        var buffer = new ArrayBufferWriter<byte>();
        serializer.Serialize(buffer, original);

        // Guid-backed ids serialize as the canonical 16-byte big-endian Guid form.
        Assert.Equal(16, buffer.WrittenCount);

        var roundTripped = serializer.Deserialize(buffer.WrittenSpan);
        Assert.Equal(original, roundTripped);
    }

    [Fact]
    public void Uuid7_RoundTrips_ThroughISerializer()
    {
        ISerializer<SerUuid7Id> serializer = new SerUuid7Id.TypedIdSerializer();
        var original = SerUuid7Id.New();

        var buffer = new ArrayBufferWriter<byte>();
        serializer.Serialize(buffer, original);

        Assert.Equal(16, buffer.WrittenCount);

        var roundTripped = serializer.Deserialize(buffer.WrittenSpan);
        Assert.Equal(original, roundTripped);
    }

    [Fact]
    public void Snowflake_RoundTrips_ThroughISerializer()
    {
        ISerializer<SerSnowflakeId> serializer = new SerSnowflakeId.TypedIdSerializer();
        var original = SerSnowflakeId.New();

        var buffer = new ArrayBufferWriter<byte>();
        serializer.Serialize(buffer, original);

        // Int64-backed ids serialize as 8 little-endian bytes.
        Assert.Equal(8, buffer.WrittenCount);

        var roundTripped = serializer.Deserialize(buffer.WrittenSpan);
        Assert.Equal(original, roundTripped);
    }

    [Fact]
    public void Sequential_RoundTrips_ThroughISerializer()
    {
        SequentialCore.Reset();
        ISerializer<SerSequentialId> serializer = new SerSequentialId.TypedIdSerializer();
        var original = SerSequentialId.New();

        var buffer = new ArrayBufferWriter<byte>();
        serializer.Serialize(buffer, original);

        Assert.Equal(8, buffer.WrittenCount);

        var roundTripped = serializer.Deserialize(buffer.WrittenSpan);
        Assert.Equal(original, roundTripped);
    }

    [Fact]
    public void Guid_Serializer_ProducesCanonicalGuidBytes()
    {
        // Sanity check: the byte form must match Guid.TryWriteBytes so
        // adapter packages and external serializers can interoperate.
        ISerializer<SerUuid7Id> serializer = new SerUuid7Id.TypedIdSerializer();
        var id = SerUuid7Id.New();

        var buffer = new ArrayBufferWriter<byte>();
        serializer.Serialize(buffer, id);

        Span<byte> expected = stackalloc byte[16];
        Assert.True(id.Value.TryWriteBytes(expected));

        Assert.Equal(expected.ToArray(), buffer.WrittenSpan.ToArray());
    }

    [Fact]
    public void Int64_Serializer_DeserializeRejectsTooSmallBuffer()
    {
        ISerializer<SerSequentialId> serializer = new SerSequentialId.TypedIdSerializer();
        var tooSmall = new byte[4];

        Assert.Throws<ArgumentException>(() => serializer.Deserialize(tooSmall));
    }

    [Fact]
    public void Guid_Serializer_DeserializeRejectsTooSmallBuffer()
    {
        ISerializer<SerUlidId> serializer = new SerUlidId.TypedIdSerializer();
        var tooSmall = new byte[8];

        Assert.Throws<ArgumentException>(() => serializer.Deserialize(tooSmall));
    }

    private sealed class StubProv : ISnowflakeWorkerIdProvider
    {
        public StubProv(int id) => WorkerId = id;
        public int WorkerId { get; }
    }
}
