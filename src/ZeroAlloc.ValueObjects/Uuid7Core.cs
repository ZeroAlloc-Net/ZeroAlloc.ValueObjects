using System;
using System.Security.Cryptography;

namespace ZeroAlloc.ValueObjects;

/// <summary>
/// Low-level, allocation-free UUIDv7 primitives per RFC 9562 §5.7. The 16-byte
/// layout is big-endian: a 48-bit Unix-millisecond timestamp in bytes 0-5, a
/// 4-bit version (<c>0x7</c>) in the high nibble of byte 6, a 12-bit
/// <c>rand_a</c> field spanning the low nibble of byte 6 and byte 7, a 2-bit
/// variant (<c>0b10</c>) in the top of byte 8, and 62 bits of <c>rand_b</c>
/// spanning the bottom of byte 8 through byte 15.
/// </summary>
/// <remarks>
/// <para>
/// Because the leading 48 bits are a big-endian Unix-millisecond timestamp,
/// two UUIDv7 values generated in different milliseconds compare in time
/// order when their 16-byte big-endian views are lexicographically compared.
/// Within a single millisecond the ordering is defined by the 74 random bits
/// and is therefore not guaranteed; callers that require strict monotonicity
/// at sub-millisecond resolution should use <see cref="UlidCore"/> instead
/// (which reuses and increments the random tail under clock ties).
/// </para>
/// <para>
/// Reference: <see href="https://www.rfc-editor.org/rfc/rfc9562#name-uuid-version-7"/>.
/// </para>
/// </remarks>
public static class Uuid7Core
{
    // netstandard2.0 has no RandomNumberGenerator.Fill(Span<byte>) overload; we keep
    // a single shared cryptographic RNG (documented thread-safe) and a per-thread
    // 10-byte scratch buffer so the random-fill path allocates nothing after the
    // first call per thread.
    private static readonly RandomNumberGenerator Rng = RandomNumberGenerator.Create();

    [ThreadStatic]
    private static byte[]? t_randomScratch;

    /// <summary>
    /// Generates a new UUIDv7-shaped <see cref="Guid"/>. The first 6 bytes
    /// (big-endian) encode the current Unix-millisecond timestamp; the remaining
    /// 10 bytes are filled from <see cref="RandomNumberGenerator"/>, after which
    /// the version (<c>0x7</c>) and variant (<c>0b10</c>) bits are overwritten per
    /// RFC 9562 §5.7.
    /// </summary>
    /// <returns>A new UUIDv7 packaged as a <see cref="Guid"/>.</returns>
    public static Guid NewGuid()
    {
        Span<byte> buffer = stackalloc byte[16];

        // 48-bit Unix timestamp, big-endian in bytes 0-5.
        long ms = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        buffer[0] = (byte)(ms >> 40);
        buffer[1] = (byte)(ms >> 32);
        buffer[2] = (byte)(ms >> 24);
        buffer[3] = (byte)(ms >> 16);
        buffer[4] = (byte)(ms >> 8);
        buffer[5] = (byte)ms;

        // 80 random bits in bytes 6-15; the version and variant nibbles are
        // overwritten below.
        byte[] scratch = t_randomScratch ??= new byte[10];
        lock (Rng)
        {
            Rng.GetBytes(scratch);
        }
        for (int i = 0; i < 10; i++)
        {
            buffer[6 + i] = scratch[i];
        }

        // Version nibble 0x7 in the high nibble of byte 6 (RFC 9562 §4.2).
        buffer[6] = (byte)((buffer[6] & 0x0F) | 0x70);

        // Variant 0b10 in the top two bits of byte 8 (RFC 9562 §4.1).
        buffer[8] = (byte)((buffer[8] & 0x3F) | 0x80);

        return GuidBigEndianHelpers.BigEndianBytesToGuid(buffer);
    }

    /// <summary>
    /// Writes the RFC 9562 big-endian 16-byte representation of
    /// <paramref name="value"/> into <paramref name="destination"/>, which must
    /// be at least 16 bytes long. Exposed to permit callers to inspect version
    /// and variant nibbles directly or to compare two UUIDv7 values for time
    /// order via raw byte comparison.
    /// </summary>
    /// <param name="value">The UUIDv7-shaped <see cref="Guid"/> to serialise.</param>
    /// <param name="destination">The destination buffer (at least 16 bytes).</param>
    public static void WriteBigEndianBytes(Guid value, Span<byte> destination)
    {
        GuidBigEndianHelpers.GuidToBigEndianBytes(value, destination);
    }
}
