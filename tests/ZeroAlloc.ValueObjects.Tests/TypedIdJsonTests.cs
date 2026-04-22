using System;
using System.Text.Json;
using ZeroAlloc.ValueObjects;

namespace ZeroAlloc.ValueObjects.Tests;

// These TypedId structs are produced by the source generator at compile time; the
// partial declarations below supply nothing more than an anchor for the attribute.
// MA0048: co-locating several TypedId anchors in one test file is intentional.
// MA0097: generated IComparable<T> does not provide comparison operators (by design:
// id types are sortable but not ordered for arithmetic semantics).
#pragma warning disable MA0048, MA0097

[TypedId(Strategy = IdStrategy.Ulid)]
public readonly partial record struct JsonUlidId;

[TypedId(Strategy = IdStrategy.Uuid7)]
public readonly partial record struct JsonUuid7Id;

[TypedId(Strategy = IdStrategy.Snowflake)]
public readonly partial record struct JsonSnowflakeId;

[TypedId(Strategy = IdStrategy.Sequential)]
public readonly partial record struct JsonSequentialId;

#pragma warning restore MA0048, MA0097

// Shares a collection with other tests that mutate TypedIdRuntime.SnowflakeProvider to
// avoid parallel-class races on the static provider slot.
[Collection("SnowflakeProviderMutation")]
public sealed class TypedIdJsonTests : IDisposable
{
    private readonly ISnowflakeWorkerIdProvider? _originalProvider;

    public TypedIdJsonTests()
    {
        _originalProvider = TypedIdRuntime.SnowflakeProvider;
        TypedIdRuntime.SnowflakeProvider = new StubProv(7);
    }

    public void Dispose() => TypedIdRuntime.SnowflakeProvider = _originalProvider;

    [Fact]
    public void Ulid_SerializesAsBase32String()
    {
        var id = JsonUlidId.New();
        var json = JsonSerializer.Serialize(id);
        // Base32 ULID is a 26-char string wrapped in quotes -> 28 chars in JSON.
        Assert.Equal(28, json.Length);
        Assert.StartsWith("\"", json, StringComparison.Ordinal);
        Assert.EndsWith("\"", json, StringComparison.Ordinal);
        Assert.Equal($"\"{id.ToString()}\"", json);
    }

    [Fact]
    public void Uuid7_SerializesAsHyphenatedUuidString()
    {
        var id = JsonUuid7Id.New();
        var json = JsonSerializer.Serialize(id);
        Assert.Equal($"\"{id.ToString()}\"", json);
        Assert.Equal(38, json.Length);  // 36 char UUID + 2 quotes
    }

    [Fact]
    public void Snowflake_SerializesAsDecimalString()
    {
        var id = JsonSnowflakeId.New();
        var json = JsonSerializer.Serialize(id);
        Assert.Equal($"\"{id.ToString()}\"", json);
    }

    [Fact]
    public void Sequential_SerializesAsDecimalString()
    {
        SequentialCore.Reset();
        var id = JsonSequentialId.New();
        var json = JsonSerializer.Serialize(id);
        Assert.Equal($"\"{id.ToString()}\"", json);
    }

    [Fact]
    public void Ulid_RoundTrips_ThroughJson()
    {
        var original = JsonUlidId.New();
        var json = JsonSerializer.Serialize(original);
        var roundTripped = JsonSerializer.Deserialize<JsonUlidId>(json);
        Assert.Equal(original, roundTripped);
    }

    [Fact]
    public void Uuid7_RoundTrips_ThroughJson()
    {
        var original = JsonUuid7Id.New();
        var json = JsonSerializer.Serialize(original);
        var roundTripped = JsonSerializer.Deserialize<JsonUuid7Id>(json);
        Assert.Equal(original, roundTripped);
    }

    [Fact]
    public void Snowflake_RoundTrips_ThroughJson()
    {
        var original = JsonSnowflakeId.New();
        var json = JsonSerializer.Serialize(original);
        var roundTripped = JsonSerializer.Deserialize<JsonSnowflakeId>(json);
        Assert.Equal(original, roundTripped);
    }

    [Fact]
    public void Sequential_RoundTrips_ThroughJson()
    {
        SequentialCore.Reset();
        var original = JsonSequentialId.New();
        var json = JsonSerializer.Serialize(original);
        var roundTripped = JsonSerializer.Deserialize<JsonSequentialId>(json);
        Assert.Equal(original, roundTripped);
    }

    [Fact]
    public void Deserialize_InvalidValue_ThrowsJsonException()
    {
        // Malformed base32 for Ulid
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<JsonUlidId>("\"invalid!\""));
    }

    [Fact]
    public void Deserialize_FromObject_WithTypedIdProperty()
    {
        var idA = JsonUlidId.New();
        var idB = JsonSnowflakeId.New();
        var envelope = new { Order = idA, Message = idB };
        var json = JsonSerializer.Serialize(envelope);

        // Deserialize back
        var back = JsonSerializer.Deserialize<Envelope>(json);
        Assert.NotNull(back);
        Assert.Equal(idA, back!.Order);
        Assert.Equal(idB, back.Message);
    }

    private sealed record Envelope(JsonUlidId Order, JsonSnowflakeId Message);

    private sealed class StubProv : ISnowflakeWorkerIdProvider
    {
        public StubProv(int id) => WorkerId = id;
        public int WorkerId { get; }
    }
}
