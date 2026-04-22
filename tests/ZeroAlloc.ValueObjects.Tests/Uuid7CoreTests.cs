namespace ZeroAlloc.ValueObjects.Tests;

public sealed class Uuid7CoreTests
{
    [Fact]
    public void NewGuid_SetsVersion7AndVariant10()
    {
        var g = Uuid7Core.NewGuid();
        Span<byte> bytes = stackalloc byte[16];
        Uuid7Core.WriteBigEndianBytes(g, bytes);
        Assert.Equal(0x70, bytes[6] & 0xF0);   // version nibble at byte 6 high
        Assert.Equal(0x80, bytes[8] & 0xC0);   // variant 10 at byte 8 top 2 bits
    }

    [Fact]
    public void NewGuid_BurstOf1000_TimeOrdered()
    {
        // UUIDv7's first 48 bits are unix_ms big-endian. With millisecond-granularity time
        // order, consecutive calls in the same ms are not required to be ordered (unlike ULID).
        // Assert the WEAKER property: the timestamp prefix across the burst is non-decreasing.
        Span<byte> prev = stackalloc byte[6];
        Span<byte> curr = stackalloc byte[16];
        var first = Uuid7Core.NewGuid();
        Uuid7Core.WriteBigEndianBytes(first, curr);
        curr.Slice(0, 6).CopyTo(prev);

        for (int i = 0; i < 1000; i++)
        {
            var next = Uuid7Core.NewGuid();
            Uuid7Core.WriteBigEndianBytes(next, curr);
            Assert.True(MemoryCompare(prev, curr.Slice(0, 6)) <= 0,
                $"Timestamp went backwards at iteration {i}");
            curr.Slice(0, 6).CopyTo(prev);
        }
    }

    private static int MemoryCompare(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        for (int i = 0; i < a.Length && i < b.Length; i++)
            if (a[i] != b[i]) return a[i].CompareTo(b[i]);
        return a.Length.CompareTo(b.Length);
    }
}
