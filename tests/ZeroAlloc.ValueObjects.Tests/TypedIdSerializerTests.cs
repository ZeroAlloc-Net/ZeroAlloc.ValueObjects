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

// Snowflake id generation touches process-wide static state: the provider slot in
// TypedIdRuntime and SnowflakeCore's packed timestamp/sequence plus MaxSpinWaitMs.
// Every class touching either shares this single collection - two differently-named
// collections still run in parallel with each other, which is what let a test that
// deliberately poisons SnowflakeCore._state break a concurrent id generation.
[Collection("SnowflakeStatics")]
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

        // Guid-backed ids serialize as 16 bytes in RFC 9562 big-endian layout.
        Assert.Equal(16, buffer.WrittenCount);

        var roundTripped = serializer.Deserialize(buffer.WrittenSpan);
        Assert.Equal(original, roundTripped);
    }

    [Fact]
    public void GuidBackedId_WritesTheRfc9562BigEndianLayout()
    {
        // Pins the wire format: the 16 bytes in RFC 9562 order, the same order the ULID and
        // UUIDv7 strategies define their values in. 2.x wrote the Guid.ToByteArray layout,
        // with Data1, Data2 and Data3 little-endian; 3.0 changed it on purpose.
        ISerializer<SerUuid7Id> serializer = new SerUuid7Id.TypedIdSerializer();
        var id = new SerUuid7Id(new Guid("00112233-4455-6677-8899-aabbccddeeff"));

        var buffer = new ArrayBufferWriter<byte>();
        serializer.Serialize(buffer, id);

        Assert.Equal(Convert.FromHexString("00112233445566778899AABBCCDDEEFF"), buffer.WrittenSpan.ToArray());
        Assert.Equal(id, serializer.Deserialize(buffer.WrittenSpan));
    }

    [Fact]
    public void Uuid7_SerializedBytes_SortInCreationOrder()
    {
        // Big-endian bytes put the UUIDv7 timestamp first, so byte order is creation order.
        ISerializer<SerUuid7Id> serializer = new SerUuid7Id.TypedIdSerializer();
        var earlier = new SerUuid7Id(new Guid("019000ff-0000-7000-8000-000000000000"));
        var later = new SerUuid7Id(new Guid("01910000-0000-7000-8000-000000000000"));

        var first = new ArrayBufferWriter<byte>();
        var second = new ArrayBufferWriter<byte>();
        serializer.Serialize(first, earlier);
        serializer.Serialize(second, later);

        Assert.True(first.WrittenSpan.SequenceCompareTo(second.WrittenSpan) < 0);
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
        // The byte form must match Guid.TryWriteBytes with bigEndian: true, so adapter
        // packages and readers on other platforms that expect RFC 9562 bytes interoperate.
        ISerializer<SerUuid7Id> serializer = new SerUuid7Id.TypedIdSerializer();
        var id = SerUuid7Id.New();

        var buffer = new ArrayBufferWriter<byte>();
        serializer.Serialize(buffer, id);

        Span<byte> expected = stackalloc byte[16];
        Assert.True(id.Value.TryWriteBytes(expected, bigEndian: true, out var written));
        Assert.Equal(16, written);

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
