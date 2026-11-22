namespace ZeroAlloc.ValueObjects.Tests;

public sealed class UlidCoreTests
{
    [Fact]
    public void ToBase32_RoundTrips_ThroughTryFromBase32()
    {
        var g = UlidCore.NewGuid();
        var s = UlidCore.ToBase32(g);
        Assert.Equal(26, s.Length);
        Assert.True(UlidCore.TryFromBase32(s.AsSpan(), out var g2));
        Assert.Equal(g, g2);
    }

    [Fact]
    public void ToBase32_Output_IsCrockfordAlphabetOnly()
    {
        var s = UlidCore.ToBase32(UlidCore.NewGuid());
        // Crockford alphabet: 0-9 A-Z except I, L, O, U
        foreach (var c in s)
        {
            Assert.True((c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z'),
                $"Character '{c}' not in base32 range");
            Assert.DoesNotContain(c, "ILOU");
        }
    }

    [Fact]
    public void NewGuid_BurstOf1000_NonDecreasingByTimestamp()
    {
        // ULIDs generated in the same ms may have random-only ordering
        // but their 48-bit timestamp prefix must be non-decreasing across the burst.
        var prev = UlidCore.NewGuid();
        for (int i = 0; i < 999; i++)
        {
            var next = UlidCore.NewGuid();
            Assert.True(string.CompareOrdinal(UlidCore.ToBase32(prev), UlidCore.ToBase32(next)) <= 0);
            prev = next;
        }
    }

    [Fact]
    public void TryFromBase32_SpecExample_DecodesToTheBigEndianGuid()
    {
        // The ulid/spec example. Its 128 bits, read big-endian, are the canonical Guid string
        // below; pins the byte layout of the Guid the decoder returns.
        Assert.True(UlidCore.TryFromBase32("01ARZ3NDEKTSV4RRFFQ69G5FAV".AsSpan(), out var value));
        Assert.Equal(new Guid("01563e3a-b5d3-d676-4c61-efb99302bd5b"), value);
        Assert.Equal("01ARZ3NDEKTSV4RRFFQ69G5FAV", UlidCore.ToBase32(value));
    }

    [Fact]
    public void NewGuid_Burst_IsStrictlyIncreasing()
    {
        // The monotonic factory increments the random tail within one millisecond, so each
        // value sorts strictly after the previous one.
        var prev = UlidCore.ToBase32(UlidCore.NewGuid());
        for (int i = 0; i < 999; i++)
        {
            var next = UlidCore.ToBase32(UlidCore.NewGuid());
            Assert.True(string.CompareOrdinal(prev, next) < 0, $"{next} does not sort after {prev}");
            prev = next;
        }
    }

    [Fact]
    public void TryFromBase32_RejectsInvalidLength() =>
        Assert.False(UlidCore.TryFromBase32("01ARZ3NDEKTSV4RRFFQ69G5FA".AsSpan(), out _));

    [Fact]
    public void TryFromBase32_RejectsInvalidCharacter() =>
        Assert.False(UlidCore.TryFromBase32("I1ARZ3NDEKTSV4RRFFQ69G5FAV".AsSpan(), out _));

    [Fact]
    public void TryFromBase32_AcceptsLowercase()
    {
        var g = UlidCore.NewGuid();
        var upper = UlidCore.ToBase32(g);
        var lower = upper.ToLowerInvariant();
        Assert.True(UlidCore.TryFromBase32(lower.AsSpan(), out var g2));
        Assert.Equal(g, g2);
    }

    [Fact]
    public void TryFormatBase32_WritesTheToBase32Characters()
    {
        var g = UlidCore.NewGuid();
        Span<char> destination = stackalloc char[40];
        Assert.True(UlidCore.TryFormatBase32(g, destination, out var written));
        Assert.Equal(26, written);
        Assert.Equal(UlidCore.ToBase32(g), destination.Slice(0, written).ToString());
    }

    [Fact]
    public void TryFormatBase32_ReturnsFalse_WhenDestinationIsTooShort()
    {
        Span<char> destination = stackalloc char[25];
        Assert.False(UlidCore.TryFormatBase32(UlidCore.NewGuid(), destination, out var written));
        Assert.Equal(0, written);
    }
}
