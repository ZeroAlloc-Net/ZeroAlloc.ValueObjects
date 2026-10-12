using System;
using System.Buffers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ZeroAlloc.ValueObjects;
using ZeroAlloc.ValueObjects.AotSmoke;

// Exercise every generator-emitted surface we care about under PublishAot=true:
//  * ULID, UUIDv7, Snowflake and Sequential TypedIds: New, ToString, TryParse.
//    Snowflake takes its worker id from ZA_SNOWFLAKE_WORKER_ID, the path a host-less
//    process uses.
//  * Every TypedId's nested TypedIdJsonConverter, driven directly through Utf8JsonWriter and
//    Utf8JsonReader so no reflection-based JsonSerializer path is involved.
//  * The nested ZeroAlloc.Serialisation TypedIdSerializer, Guid- and Int64-backed.
//  * ZeroAlloc.ValueObjects.EfCore: the generated converter registration, a model build and a
//    SQLite round-trip; see EfCoreSmoke.
//  * A [ValueObject] class — structural equality + GetHashCode + ToString.
//
// Each block returns 1 on failure so we get a single exit code for CI to gate on.

var a = OrderId.New();
var b = OrderId.New();
if (a == b) return Fail("OrderId.New() produced duplicate values");

var aStr = a.ToString();
if (aStr.Length != 26) return Fail($"OrderId ToString expected 26 chars, got {aStr.Length}");
if (!OrderId.TryParse(aStr, null, out var aRound) || aRound != a)
    return Fail("OrderId round-trip via TryParse failed");

var m = MessageId.New();
var mStr = m.ToString();
if (mStr.Length != 36) return Fail($"MessageId ToString expected 36 chars, got {mStr.Length}");
if (!MessageId.TryParse(mStr, null, out var mRound) || mRound != m)
    return Fail("MessageId round-trip via TryParse failed");

var s1 = SequenceId.New();
var s2 = SequenceId.New();
// Sequential ids must strictly increase — two adjacent New() calls on the same
// thread should never collide.
if (s1 == s2) return Fail("SequenceId.New() produced duplicates");

Environment.SetEnvironmentVariable("ZA_SNOWFLAKE_WORKER_ID", "7");
var f1 = SnowflakeMessageId.New();
var f2 = SnowflakeMessageId.New();
if (f2.Value <= f1.Value) return Fail("SnowflakeMessageId.New() did not increase");
if (SnowflakeCore.ExtractWorkerId(f1.Value) != 7) return Fail("SnowflakeMessageId worker id was not read from the environment");
if (!SnowflakeMessageId.TryParse(f1.ToString(), null, out var fRound) || fRound != f1)
    return Fail("SnowflakeMessageId round-trip via TryParse failed");

// JSON converters: write produces the ToString form, read returns the same id, also when the
// value is escaped, and invalid input is rejected with JsonException.
if (JsonRoundTrip(new OrderId.TypedIdJsonConverter(), a, aStr) is { } e1) return Fail(e1);
if (JsonRoundTrip(new MessageId.TypedIdJsonConverter(), m, mStr) is { } e2) return Fail(e2);
if (JsonRoundTrip(new SnowflakeMessageId.TypedIdJsonConverter(), f1, f1.ToString()) is { } e3) return Fail(e3);
if (JsonRoundTrip(new SequenceId.TypedIdJsonConverter(), s2, s2.ToString()) is { } e4) return Fail(e4);

// ZeroAlloc.Serialisation integration.
var bytes = new ArrayBufferWriter<byte>();
var orderSerializer = new OrderId.TypedIdSerializer();
orderSerializer.Serialize(bytes, a);
if (bytes.WrittenCount != 16 || orderSerializer.Deserialize(bytes.WrittenSpan) != a)
    return Fail("OrderId TypedIdSerializer round-trip failed");
bytes.ResetWrittenCount();
var snowflakeSerializer = new SnowflakeMessageId.TypedIdSerializer();
snowflakeSerializer.Serialize(bytes, f1);
if (bytes.WrittenCount != 8 || snowflakeSerializer.Deserialize(bytes.WrittenSpan) != f1)
    return Fail("SnowflakeMessageId TypedIdSerializer round-trip failed");

// ValueObject: two separately-constructed instances with identical state must be equal
// and produce the same hash. This path is entirely generator-emitted, no reflection.
var x = new Money("EUR", 42.5m);
var y = new Money("EUR", 42.5m);
var z = new Money("USD", 42.5m);
if (!x.Equals(y)) return Fail("Money equality: identical state not equal");
if (x.GetHashCode() != y.GetHashCode()) return Fail("Money hash: identical state produced different hashes");
if (x.Equals(z)) return Fail("Money equality: different currency compared equal");

// ZeroAlloc.ValueObjects.EfCore: generated converter registration and a SQLite round-trip.
if (EfCoreSmoke.Run() is { } efError) return Fail(efError);

Console.WriteLine("AOT smoke: PASS");
return 0;

static string? JsonRoundTrip<T>(JsonConverter<T> converter, T id, string expected)
    where T : IEquatable<T>
{
    var buffer = new ArrayBufferWriter<byte>();
    using (var writer = new Utf8JsonWriter(buffer))
    {
        converter.Write(writer, id, ConverterOptions());
    }
    var json = Encoding.UTF8.GetString(buffer.WrittenSpan);
    if (!string.Equals(json, "\"" + expected + "\"", StringComparison.Ordinal)) return $"{typeof(T).Name} JSON write produced {json}";

    if (!Read(converter, buffer.WrittenSpan.ToArray()).Equals(id)) return $"{typeof(T).Name} JSON read failed";

    var escaped = new StringBuilder("\"");
    foreach (var c in expected) escaped.Append("\\u").Append(((int)c).ToString("X4", System.Globalization.CultureInfo.InvariantCulture));
    escaped.Append('"');
    if (!Read(converter, Encoding.UTF8.GetBytes(escaped.ToString())).Equals(id))
        return $"{typeof(T).Name} escaped JSON read failed";

    try
    {
        Read(converter, Encoding.UTF8.GetBytes("\"not an id!\""));
        return $"{typeof(T).Name} JSON read accepted an invalid value";
    }
    catch (JsonException)
    {
        return null;
    }
}

// The converters never consult the options. A fresh instance carries no reflection-based type
// resolver, unlike JsonSerializerOptions.Default, so using it raises no trim or AOT warning.
static JsonSerializerOptions ConverterOptions() => new();

static T Read<T>(JsonConverter<T> converter, byte[] json)
{
    var reader = new Utf8JsonReader(json);
    reader.Read();
    return converter.Read(ref reader, typeof(T), ConverterOptions())!;
}

static int Fail(string message)
{
    Console.Error.WriteLine($"AOT smoke: FAIL — {message}");
    return 1;
}
