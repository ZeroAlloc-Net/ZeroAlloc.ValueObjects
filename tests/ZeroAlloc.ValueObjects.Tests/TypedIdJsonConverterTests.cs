using System;
using System.Buffers;
using System.Text;
using System.Text.Json;
using ZeroAlloc.TestHelpers;
using ZeroAlloc.ValueObjects;

namespace ZeroAlloc.ValueObjects.Tests;

// These TypedId structs are produced by the source generator at compile time; the
// partial declarations below supply nothing more than an anchor for the attribute.
// MA0048: co-locating several TypedId anchors in one test file is intentional.
#pragma warning disable MA0048

[TypedId(Strategy = IdStrategy.Ulid)]
public readonly partial record struct ConvUlidId;

[TypedId(Strategy = IdStrategy.Uuid7)]
public readonly partial record struct ConvUuid7Id;

[TypedId(Strategy = IdStrategy.Snowflake)]
public readonly partial record struct ConvSnowflakeId;

[TypedId(Strategy = IdStrategy.Sequential)]
public readonly partial record struct ConvSequentialId;

#pragma warning restore MA0048

// Drives the generated TypedIdJsonConverter directly, so the allocation gates measure the
// converter and nothing of JsonSerializer's own machinery.
public sealed class TypedIdJsonConverterTests
{
    private static readonly JsonSerializerOptions Options = JsonSerializerOptions.Default;

    private static readonly ConvUlidId Ulid = ConvUlidId.New();
    private static readonly ConvUuid7Id Uuid7 = ConvUuid7Id.New();
    private static readonly ConvSnowflakeId Snowflake = new(123_456_789_012_345_678L);
    private static readonly ConvSequentialId Sequential = new(1_234_567_890_123L);

    // ---- allocation gates ---------------------------------------------------------------

    [Fact]
    public void Read_Ulid_DoesNotAllocate() =>
        AssertReadIsAllocationFree(new ConvUlidId.TypedIdJsonConverter(), Ulid, Quoted(Ulid.ToString()));

    [Fact]
    public void Read_Uuid7_DoesNotAllocate() =>
        AssertReadIsAllocationFree(new ConvUuid7Id.TypedIdJsonConverter(), Uuid7, Quoted(Uuid7.ToString()));

    [Fact]
    public void Read_Snowflake_DoesNotAllocate() =>
        AssertReadIsAllocationFree(new ConvSnowflakeId.TypedIdJsonConverter(), Snowflake, Quoted(Snowflake.ToString()));

    [Fact]
    public void Read_Sequential_DoesNotAllocate() =>
        AssertReadIsAllocationFree(new ConvSequentialId.TypedIdJsonConverter(), Sequential, Quoted(Sequential.ToString()));

    [Fact]
    public void Read_EscapedValue_DoesNotAllocate() =>
        AssertReadIsAllocationFree(new ConvSequentialId.TypedIdJsonConverter(), Sequential, Quoted(Escape(Sequential.ToString())));

    // Fully escaped, a ULID is 156 bytes of JSON and a UUID 216: longer than any small stack
    // buffer although the values themselves are short.
    [Fact]
    public void Read_FullyEscapedUlid_DoesNotAllocate() =>
        AssertReadIsAllocationFree(new ConvUlidId.TypedIdJsonConverter(), Ulid, Quoted(Escape(Ulid.ToString())));

    [Fact]
    public void Read_FullyEscapedUuid7_DoesNotAllocate() =>
        AssertReadIsAllocationFree(new ConvUuid7Id.TypedIdJsonConverter(), Uuid7, Quoted(Escape(Uuid7.ToString())));

    // Input longer than the stack buffer borrows from ArrayPool. AllocationGate forces a full GC
    // after warm-up, which lets the pool drop its cached array, so the first rent afterwards
    // allocates once. What matters is the steady state: no allocation per call.
    [Fact]
    public void Read_LongPaddedValue_DoesNotAllocateInSteadyState()
    {
        var converter = new ConvSequentialId.TypedIdJsonConverter();
        var bytes = Encoding.UTF8.GetBytes(Quoted(new string(' ', 300) + Sequential.ToString() + new string(' ', 300)));

        ConvSequentialId ReadOnce()
        {
            var reader = new Utf8JsonReader(bytes);
            reader.Read();
            return converter.Read(ref reader, typeof(ConvSequentialId), Options);
        }

        Assert.Equal(Sequential, ReadOnce());
        for (var i = 0; i < 10; i++) ReadOnce();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++) ReadOnce();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void Write_Ulid_DoesNotAllocate() =>
        AssertWriteIsAllocationFree(new ConvUlidId.TypedIdJsonConverter(), Ulid);

    [Fact]
    public void Write_Uuid7_DoesNotAllocate() =>
        AssertWriteIsAllocationFree(new ConvUuid7Id.TypedIdJsonConverter(), Uuid7);

    [Fact]
    public void Write_Snowflake_DoesNotAllocate() =>
        AssertWriteIsAllocationFree(new ConvSnowflakeId.TypedIdJsonConverter(), Snowflake);

    [Fact]
    public void Write_Sequential_DoesNotAllocate() =>
        AssertWriteIsAllocationFree(new ConvSequentialId.TypedIdJsonConverter(), Sequential);

    // ---- output and input parity ---------------------------------------------------------

    [Fact]
    public void Write_EmitsToStringForm_ForEveryKind()
    {
        Assert.Equal(Quoted(Ulid.ToString()), Write(new ConvUlidId.TypedIdJsonConverter(), Ulid));
        Assert.Equal(Quoted(Uuid7.ToString()), Write(new ConvUuid7Id.TypedIdJsonConverter(), Uuid7));
        Assert.Equal(Quoted(Snowflake.ToString()), Write(new ConvSnowflakeId.TypedIdJsonConverter(), Snowflake));
        Assert.Equal(Quoted(Sequential.ToString()), Write(new ConvSequentialId.TypedIdJsonConverter(), Sequential));
        Assert.Equal("\"-42\"", Write(new ConvSequentialId.TypedIdJsonConverter(), new ConvSequentialId(-42)));
        Assert.Equal(
            Quoted(long.MinValue.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            Write(new ConvSequentialId.TypedIdJsonConverter(), new ConvSequentialId(long.MinValue)));
    }

    [Fact]
    public void Read_EscapedValue_ParsesLikeTheUnescapedString()
    {
        Assert.Equal(Ulid, Read(new ConvUlidId.TypedIdJsonConverter(), Quoted(Escape(Ulid.ToString()))));
        Assert.Equal(Uuid7, Read(new ConvUuid7Id.TypedIdJsonConverter(), Quoted(Escape(Uuid7.ToString()))));
    }

    [Fact]
    public void Read_MultiSegmentValue_ParsesLikeTheContiguousString()
    {
        Assert.Equal(Ulid, ReadSegmented(new ConvUlidId.TypedIdJsonConverter(), Quoted(Ulid.ToString())));
        Assert.Equal(Uuid7, ReadSegmented(new ConvUuid7Id.TypedIdJsonConverter(), Quoted(Escape(Uuid7.ToString()))));
        Assert.Equal(Snowflake, ReadSegmented(new ConvSnowflakeId.TypedIdJsonConverter(), Quoted(Snowflake.ToString())));
    }

    [Fact]
    public void Read_LongPaddedValue_StillParses_LikeTryParseOnTheString()
    {
        // NumberStyles.Integer and Guid parsing both accept surrounding whitespace, so a valid
        // value can be longer than any fixed stack buffer. It must keep parsing.
        var padded = new string(' ', 300) + Sequential.ToString() + new string(' ', 300);
        Assert.True(ConvSequentialId.TryParse(padded, null, out var expected));
        Assert.Equal(expected, Read(new ConvSequentialId.TypedIdJsonConverter(), Quoted(padded)));

        var paddedGuid = new string(' ', 300) + Uuid7.ToString() + new string(' ', 300);
        Assert.True(ConvUuid7Id.TryParse(paddedGuid, null, out var expectedGuid));
        Assert.Equal(expectedGuid, Read(new ConvUuid7Id.TypedIdJsonConverter(), Quoted(paddedGuid)));
    }

    // ---- error behaviour is unchanged -----------------------------------------------------

    [Fact]
    public void Read_InvalidString_ThrowsJsonException()
    {
        Assert.Throws<JsonException>(() => Read(new ConvUlidId.TypedIdJsonConverter(), "\"invalid!\""));
        Assert.Throws<JsonException>(() => Read(new ConvUuid7Id.TypedIdJsonConverter(), "\"not-a-guid\""));
        Assert.Throws<JsonException>(() => Read(new ConvSnowflakeId.TypedIdJsonConverter(), "\"12x\""));
        Assert.Throws<JsonException>(() => Read(new ConvSequentialId.TypedIdJsonConverter(), "\"\""));
        Assert.Throws<JsonException>(() => Read(new ConvSequentialId.TypedIdJsonConverter(), Quoted(new string('9', 400))));
    }

    [Fact]
    public void Read_NullToken_ThrowsJsonException()
    {
        Assert.Throws<JsonException>(() => Read(new ConvUlidId.TypedIdJsonConverter(), "null"));
        Assert.Throws<JsonException>(() => Read(new ConvSnowflakeId.TypedIdJsonConverter(), "null"));
    }

    [Fact]
    public void Read_NonStringToken_ThrowsTheReadersInvalidOperationException()
    {
        // A JSON number is not accepted even for Int64-backed ids: only the string form is.
        Assert.Throws<InvalidOperationException>(() => Read(new ConvSnowflakeId.TypedIdJsonConverter(), "123"));
        Assert.Throws<InvalidOperationException>(() => Read(new ConvUlidId.TypedIdJsonConverter(), "true"));

        // Through the serializer the reader's exception surfaces as a JsonException.
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ConvSequentialId>("123"));
    }

    // ---- helpers ------------------------------------------------------------------------------

    private static string Quoted(string s) => "\"" + s + "\"";

    // Escapes every character as \uXXXX, so the reader reports ValueIsEscaped.
    private static string Escape(string s)
    {
        var sb = new StringBuilder(s.Length * 6);
        foreach (var c in s) sb.Append("\\u").Append(((int)c).ToString("X4", System.Globalization.CultureInfo.InvariantCulture));
        return sb.ToString();
    }

    private static T Read<T>(System.Text.Json.Serialization.JsonConverter<T> converter, string json)
    {
        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json));
        reader.Read();
        return converter.Read(ref reader, typeof(T), Options)!;
    }

    private static T ReadSegmented<T>(System.Text.Json.Serialization.JsonConverter<T> converter, string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        // One byte per segment: every value spans several segments.
        Segment first = new(bytes.AsMemory(0, 1));
        var last = first;
        for (var i = 1; i < bytes.Length; i++) last = last.Append(bytes.AsMemory(i, 1));
        var reader = new Utf8JsonReader(new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length));
        reader.Read();
        Assert.True(reader.HasValueSequence);
        return converter.Read(ref reader, typeof(T), Options)!;
    }

    private static string Write<T>(System.Text.Json.Serialization.JsonConverter<T> converter, T value)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            converter.Write(writer, value, Options);
        }
        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static void AssertReadIsAllocationFree<T>(System.Text.Json.Serialization.JsonConverter<T> converter, T expected, string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        Assert.Equal(expected, Read(converter, json));

        AllocationGate.AssertBudget(0, 1000, () =>
        {
            var reader = new Utf8JsonReader(bytes);
            reader.Read();
            _ = converter.Read(ref reader, typeof(T), Options);
        }, $"{typeof(T).Name}.TypedIdJsonConverter.Read");
    }

    private static void AssertWriteIsAllocationFree<T>(System.Text.Json.Serialization.JsonConverter<T> converter, T value)
    {
        var buffer = new ArrayBufferWriter<byte>(256);
        var writer = new Utf8JsonWriter(buffer);

        AllocationGate.AssertBudget(0, 1000, () =>
        {
            buffer.ResetWrittenCount();
            writer.Reset(buffer);
            converter.Write(writer, value, Options);
            writer.Flush();
        }, $"{typeof(T).Name}.TypedIdJsonConverter.Write");

        writer.Dispose();
    }

    private sealed class Segment : ReadOnlySequenceSegment<byte>
    {
        public Segment(ReadOnlyMemory<byte> memory) => Memory = memory;

        public Segment Append(ReadOnlyMemory<byte> memory)
        {
            var next = new Segment(memory) { RunningIndex = RunningIndex + Memory.Length };
            Next = next;
            return next;
        }
    }
}
