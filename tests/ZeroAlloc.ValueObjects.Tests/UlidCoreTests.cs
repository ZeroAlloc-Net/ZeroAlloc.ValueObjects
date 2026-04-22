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
}
