using System.Buffers;
using System.Text;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using ZeroAlloc.ValueObjects;

namespace ZeroAlloc.ValueObjects.Benchmarks;

// Allocation coverage for the generated serialization surfaces: the nested JSON converter, driven
// directly so JsonSerializer's own machinery stays out of the numbers, and the nested
// ZeroAlloc.Serialisation TypedIdSerializer. Every row is expected to report 0 B.
[MemoryDiagnoser]
public class TypedIdSerializationBenchmarks
{
    private static readonly JsonSerializerOptions Options = JsonSerializerOptions.Default;

    private readonly BenchUlidId _ulid = BenchUlidId.New();
    private readonly BenchUuid7Id _uuid7 = BenchUuid7Id.New();
    private readonly BenchSnowflakeId _snowflake = new(123_456_789_012_345_678L);
    private readonly BenchSequentialId _sequential = new(1_234_567_890_123L);

    private readonly BenchUlidId.TypedIdJsonConverter _ulidConverter = new();
    private readonly BenchUuid7Id.TypedIdJsonConverter _uuid7Converter = new();
    private readonly BenchSnowflakeId.TypedIdJsonConverter _snowflakeConverter = new();
    private readonly BenchSequentialId.TypedIdJsonConverter _sequentialConverter = new();

    private readonly BenchUlidId.TypedIdSerializer _ulidSerializer = new();
    private readonly BenchSnowflakeId.TypedIdSerializer _snowflakeSerializer = new();

    private readonly ArrayBufferWriter<byte> _buffer = new(256);
    private Utf8JsonWriter _writer = null!;

    private byte[] _ulidJson = null!;
    private byte[] _uuid7Json = null!;
    private byte[] _snowflakeJson = null!;
    private byte[] _ulidEscapedJson = null!;
    private byte[] _ulidBytes = null!;
    private byte[] _snowflakeBytes = null!;

    [GlobalSetup]
    public void Setup()
    {
        _writer = new Utf8JsonWriter(_buffer);
        _ulidJson = Quoted(_ulid.ToString());
        _uuid7Json = Quoted(_uuid7.ToString());
        _snowflakeJson = Quoted(_snowflake.ToString());

        // Every character written as \uXXXX, so the reader takes the escaped path.
        var escaped = new StringBuilder();
        foreach (var c in _ulid.ToString())
            escaped.Append("\\u").Append(((int)c).ToString("X4", System.Globalization.CultureInfo.InvariantCulture));
        _ulidEscapedJson = Quoted(escaped.ToString());

        var bytes = new ArrayBufferWriter<byte>();
        _ulidSerializer.Serialize(bytes, _ulid);
        _ulidBytes = bytes.WrittenSpan.ToArray();
        bytes.ResetWrittenCount();
        _snowflakeSerializer.Serialize(bytes, _snowflake);
        _snowflakeBytes = bytes.WrittenSpan.ToArray();
    }

    [GlobalCleanup]
    public void Cleanup() => _writer.Dispose();

    [Benchmark] public BenchUlidId Json_Ulid_Read() => Read(_ulidConverter, _ulidJson);
    [Benchmark] public BenchUlidId Json_Ulid_ReadEscaped() => Read(_ulidConverter, _ulidEscapedJson);
    [Benchmark] public BenchUuid7Id Json_Uuid7_Read() => Read(_uuid7Converter, _uuid7Json);
    [Benchmark] public BenchSnowflakeId Json_Snowflake_Read() => Read(_snowflakeConverter, _snowflakeJson);

    [Benchmark] public int Json_Ulid_Write() => Write(_ulidConverter, _ulid);
    [Benchmark] public int Json_Uuid7_Write() => Write(_uuid7Converter, _uuid7);
    [Benchmark] public int Json_Snowflake_Write() => Write(_snowflakeConverter, _snowflake);
    [Benchmark] public int Json_Sequential_Write() => Write(_sequentialConverter, _sequential);

    [Benchmark]
    public int Serializer_Ulid_Serialize()
    {
        _buffer.ResetWrittenCount();
        _ulidSerializer.Serialize(_buffer, _ulid);
        return _buffer.WrittenCount;
    }

    [Benchmark] public BenchUlidId Serializer_Ulid_Deserialize() => _ulidSerializer.Deserialize(_ulidBytes);

    [Benchmark]
    public int Serializer_Snowflake_Serialize()
    {
        _buffer.ResetWrittenCount();
        _snowflakeSerializer.Serialize(_buffer, _snowflake);
        return _buffer.WrittenCount;
    }

    [Benchmark] public BenchSnowflakeId Serializer_Snowflake_Deserialize() => _snowflakeSerializer.Deserialize(_snowflakeBytes);

    private static T Read<T>(System.Text.Json.Serialization.JsonConverter<T> converter, byte[] json)
    {
        var reader = new Utf8JsonReader(json);
        reader.Read();
        return converter.Read(ref reader, typeof(T), Options)!;
    }

    private int Write<T>(System.Text.Json.Serialization.JsonConverter<T> converter, T value)
    {
        _buffer.ResetWrittenCount();
        _writer.Reset(_buffer);
        converter.Write(_writer, value, Options);
        _writer.Flush();
        return _buffer.WrittenCount;
    }

    private static byte[] Quoted(string s) => Encoding.UTF8.GetBytes("\"" + s + "\"");
}
