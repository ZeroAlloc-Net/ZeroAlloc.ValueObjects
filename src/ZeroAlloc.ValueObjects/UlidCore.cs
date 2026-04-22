using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace ZeroAlloc.ValueObjects;

/// <summary>
/// Low-level, allocation-free ULID primitives: generation, Crockford base32 encoding,
/// and decoding. All 16-byte intermediate buffers are <c>stackalloc</c>; the only
/// heap allocation is the final <see cref="string"/> returned from
/// <see cref="ToBase32(Guid)"/>.
/// </summary>
/// <remarks>
/// <para>
/// A ULID is a 128-bit value laid out big-endian as: 48-bit Unix millisecond timestamp
/// (bytes 0-5) followed by 80 bits of cryptographic randomness (bytes 6-15). When
/// rendered in Crockford base32, the lexicographic order of the resulting string matches
/// the chronological order of generation, which makes ULIDs suitable for sortable
/// primary keys.
/// </para>
/// <para>
/// Reference: <see href="https://github.com/ulid/spec"/>.
/// </para>
/// </remarks>
public static class UlidCore
{
    // Crockford base32 alphabet: digits 0-9 followed by A-Z omitting I, L, O, U.
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    // netstandard2.0 has no RandomNumberGenerator.Fill(Span<byte>) overload; we keep a
    // single shared cryptographic RNG (documented thread-safe) and a per-thread 10-byte
    // scratch buffer so the random-fill path allocates nothing after the first call.
    private static readonly RandomNumberGenerator Rng = RandomNumberGenerator.Create();

    [ThreadStatic]
    private static byte[]? t_randomScratch;

    // Monotonicity state: when the wall-clock timestamp has not advanced past the
    // previous call, we reuse the previous random tail incremented by one so that the
    // output remains lexicographically non-decreasing per the ULID spec's monotonic
    // factory recommendation (https://github.com/ulid/spec#monotonicity).
    private static readonly object MonotonicGate = new object();
    private static long s_lastMs;
    private static readonly byte[] s_lastRandom = new byte[10];

    /// <summary>
    /// Generates a new ULID-shaped <see cref="Guid"/>. The first 6 bytes (big-endian)
    /// encode the current Unix-millisecond timestamp; the remaining 10 bytes are filled
    /// from <see cref="RandomNumberGenerator"/>. The produced value round-trips through
    /// <see cref="ToBase32(Guid)"/> and <see cref="TryFromBase32(ReadOnlySpan{char}, out Guid)"/>.
    /// </summary>
    /// <returns>A new ULID packaged as a <see cref="Guid"/>.</returns>
    public static Guid NewGuid()
    {
        Span<byte> buffer = stackalloc byte[16];
        long ms = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        byte[] scratch = t_randomScratch ??= new byte[10];

        lock (MonotonicGate)
        {
            if (ms > s_lastMs)
            {
                Rng.GetBytes(scratch);
                s_lastMs = ms;
            }
            else
            {
                // Same (or clock-regressed) millisecond: pin the timestamp to the
                // previous value and increment the previous random tail by one so that
                // the resulting 128-bit value is strictly greater than the previous one.
                ms = s_lastMs;
                IncrementBigEndian(s_lastRandom);
                Buffer.BlockCopy(s_lastRandom, 0, scratch, 0, 10);
            }

            Buffer.BlockCopy(scratch, 0, s_lastRandom, 0, 10);
        }

        // 48-bit Unix timestamp, big-endian in bytes 0-5.
        buffer[0] = (byte)(ms >> 40);
        buffer[1] = (byte)(ms >> 32);
        buffer[2] = (byte)(ms >> 24);
        buffer[3] = (byte)(ms >> 16);
        buffer[4] = (byte)(ms >> 8);
        buffer[5] = (byte)ms;

        // 80-bit random (or incremented previous random) in bytes 6-15.
        scratch.AsSpan().CopyTo(buffer.Slice(6, 10));

        return BigEndianBytesToGuid(buffer);
    }

    /// <summary>
    /// Increments a big-endian byte array in place by one, with overflow propagation
    /// from the least-significant byte upward. When every byte is <c>0xFF</c> the value
    /// wraps silently; the caller is responsible for ensuring this is acceptable (for
    /// 80 random bits the probability of wrap within a single millisecond is negligible).
    /// </summary>
    private static void IncrementBigEndian(byte[] bytes)
    {
        for (int i = bytes.Length - 1; i >= 0; i--)
        {
            if (++bytes[i] != 0)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Encodes a ULID-shaped <see cref="Guid"/> to its 26-character Crockford base32
    /// representation. The encoding treats the value as a big-endian 128-bit integer and
    /// emits 26 base32 digits (5 bits each, 130 bits total, top 2 bits implicit zero).
    /// </summary>
    /// <param name="value">The value to encode.</param>
    /// <returns>A 26-character Crockford base32 string.</returns>
    public static string ToBase32(Guid value)
    {
        Span<byte> b = stackalloc byte[16];
        GuidToBigEndianBytes(value, b);

        Span<char> c = stackalloc char[26];

        // Top 2 bits are implicit zero; char[0] carries the top 3 bits of byte 0.
        c[0]  = Alphabet[(b[0] & 0xE0) >> 5];
        c[1]  = Alphabet[b[0] & 0x1F];
        c[2]  = Alphabet[(b[1] & 0xF8) >> 3];
        c[3]  = Alphabet[((b[1] & 0x07) << 2) | ((b[2] & 0xC0) >> 6)];
        c[4]  = Alphabet[(b[2] & 0x3E) >> 1];
        c[5]  = Alphabet[((b[2] & 0x01) << 4) | ((b[3] & 0xF0) >> 4)];
        c[6]  = Alphabet[((b[3] & 0x0F) << 1) | ((b[4] & 0x80) >> 7)];
        c[7]  = Alphabet[(b[4] & 0x7C) >> 2];
        c[8]  = Alphabet[((b[4] & 0x03) << 3) | ((b[5] & 0xE0) >> 5)];
        c[9]  = Alphabet[b[5] & 0x1F];

        c[10] = Alphabet[(b[6] & 0xF8) >> 3];
        c[11] = Alphabet[((b[6] & 0x07) << 2) | ((b[7] & 0xC0) >> 6)];
        c[12] = Alphabet[(b[7] & 0x3E) >> 1];
        c[13] = Alphabet[((b[7] & 0x01) << 4) | ((b[8] & 0xF0) >> 4)];
        c[14] = Alphabet[((b[8] & 0x0F) << 1) | ((b[9] & 0x80) >> 7)];
        c[15] = Alphabet[(b[9] & 0x7C) >> 2];
        c[16] = Alphabet[((b[9] & 0x03) << 3) | ((b[10] & 0xE0) >> 5)];
        c[17] = Alphabet[b[10] & 0x1F];
        c[18] = Alphabet[(b[11] & 0xF8) >> 3];
        c[19] = Alphabet[((b[11] & 0x07) << 2) | ((b[12] & 0xC0) >> 6)];
        c[20] = Alphabet[(b[12] & 0x3E) >> 1];
        c[21] = Alphabet[((b[12] & 0x01) << 4) | ((b[13] & 0xF0) >> 4)];
        c[22] = Alphabet[((b[13] & 0x0F) << 1) | ((b[14] & 0x80) >> 7)];
        c[23] = Alphabet[(b[14] & 0x7C) >> 2];
        c[24] = Alphabet[((b[14] & 0x03) << 3) | ((b[15] & 0xE0) >> 5)];
        c[25] = Alphabet[b[15] & 0x1F];

        return c.ToString();
    }

    /// <summary>
    /// Attempts to decode a 26-character Crockford base32 string into a ULID-shaped
    /// <see cref="Guid"/>. Decoding is case-insensitive per the Crockford specification
    /// and rejects any character outside the alphabet as well as any input whose length
    /// is not exactly 26.
    /// </summary>
    /// <param name="s">The input span to decode.</param>
    /// <param name="value">On success, the decoded value; otherwise <see cref="Guid.Empty"/>.</param>
    /// <returns><c>true</c> when <paramref name="s"/> is a valid Crockford base32 ULID; otherwise <c>false</c>.</returns>
    public static bool TryFromBase32(ReadOnlySpan<char> s, out Guid value)
    {
        value = default;
        if (s.Length != 26)
        {
            return false;
        }

        Span<byte> d = stackalloc byte[26];
        for (int i = 0; i < 26; i++)
        {
            int v = DecodeChar(s[i]);
            if (v < 0)
            {
                return false;
            }
            d[i] = (byte)v;
        }

        // The top 2 bits of the encoded value are implicit zero; the max value of d[0]
        // is therefore 0b00111 == 7. Anything larger means the input represents a 130-bit
        // integer that overflows a 128-bit Guid.
        if (d[0] > 7)
        {
            return false;
        }

        Span<byte> b = stackalloc byte[16];

        b[0]  = (byte)((d[0]  << 5) | d[1]);
        b[1]  = (byte)((d[2]  << 3) | (d[3] >> 2));
        b[2]  = (byte)((d[3]  << 6) | (d[4] << 1) | (d[5] >> 4));
        b[3]  = (byte)((d[5]  << 4) | (d[6] >> 1));
        b[4]  = (byte)((d[6]  << 7) | (d[7] << 2) | (d[8] >> 3));
        b[5]  = (byte)((d[8]  << 5) | d[9]);
        b[6]  = (byte)((d[10] << 3) | (d[11] >> 2));
        b[7]  = (byte)((d[11] << 6) | (d[12] << 1) | (d[13] >> 4));
        b[8]  = (byte)((d[13] << 4) | (d[14] >> 1));
        b[9]  = (byte)((d[14] << 7) | (d[15] << 2) | (d[16] >> 3));
        b[10] = (byte)((d[16] << 5) | d[17]);
        b[11] = (byte)((d[18] << 3) | (d[19] >> 2));
        b[12] = (byte)((d[19] << 6) | (d[20] << 1) | (d[21] >> 4));
        b[13] = (byte)((d[21] << 4) | (d[22] >> 1));
        b[14] = (byte)((d[22] << 7) | (d[23] << 2) | (d[24] >> 3));
        b[15] = (byte)((d[24] << 5) | d[25]);

        value = BigEndianBytesToGuid(b);
        return true;
    }

    /// <summary>
    /// Decodes a single Crockford base32 character (case-insensitive) to its 5-bit value,
    /// or returns <c>-1</c> for any character outside the alphabet. Characters I, L, O, U
    /// are explicitly rejected (they are not remapped to similar digits in this implementation).
    /// </summary>
    private static int DecodeChar(char ch)
    {
        if (ch >= '0' && ch <= '9')
        {
            return ch - '0';
        }

        char u = ch;
        if (u >= 'a' && u <= 'z')
        {
            u = (char)(u - ('a' - 'A'));
        }

        // Alphabet after '9': A B C D E F G H J K M N P Q R S T V W X Y Z
        // Indices:             10 11 12 13 14 15 16 17 18 19 20 21 22 23 24 25 26 27 28 29 30 31
        switch (u)
        {
            case 'A': return 10;
            case 'B': return 11;
            case 'C': return 12;
            case 'D': return 13;
            case 'E': return 14;
            case 'F': return 15;
            case 'G': return 16;
            case 'H': return 17;
            case 'J': return 18;
            case 'K': return 19;
            case 'M': return 20;
            case 'N': return 21;
            case 'P': return 22;
            case 'Q': return 23;
            case 'R': return 24;
            case 'S': return 25;
            case 'T': return 26;
            case 'V': return 27;
            case 'W': return 28;
            case 'X': return 29;
            case 'Y': return 30;
            case 'Z': return 31;
            default:  return -1;
        }
    }

    /// <summary>
    /// Converts a 16-byte big-endian buffer into a <see cref="Guid"/> such that the
    /// companion <see cref="GuidToBigEndianBytes"/> returns the original byte sequence.
    /// On little-endian hosts the first three native fields (Data1/Data2/Data3) are
    /// byte-reversed so that <see cref="Guid.ToByteArray"/> and our big-endian view agree.
    /// </summary>
    private static Guid BigEndianBytesToGuid(ReadOnlySpan<byte> be)
    {
        Span<byte> tmp = stackalloc byte[16];
        be.CopyTo(tmp);

        if (BitConverter.IsLittleEndian)
        {
            // Reverse Data1 (bytes 0..3), Data2 (4..5), Data3 (6..7).
            byte t;
            t = tmp[0]; tmp[0] = tmp[3]; tmp[3] = t;
            t = tmp[1]; tmp[1] = tmp[2]; tmp[2] = t;
            t = tmp[4]; tmp[4] = tmp[5]; tmp[5] = t;
            t = tmp[6]; tmp[6] = tmp[7]; tmp[7] = t;
        }

        return MemoryMarshal.Read<Guid>(tmp);
    }

    /// <summary>
    /// Writes the big-endian 16-byte ULID representation of <paramref name="g"/> into
    /// <paramref name="dest"/> (which must be at least 16 bytes long). Inverse of
    /// <see cref="BigEndianBytesToGuid"/>.
    /// </summary>
    private static void GuidToBigEndianBytes(Guid g, Span<byte> dest)
    {
        MemoryMarshal.Write(dest, ref g);

        if (BitConverter.IsLittleEndian)
        {
            byte t;
            t = dest[0]; dest[0] = dest[3]; dest[3] = t;
            t = dest[1]; dest[1] = dest[2]; dest[2] = t;
            t = dest[4]; dest[4] = dest[5]; dest[5] = t;
            t = dest[6]; dest[6] = dest[7]; dest[7] = t;
        }
    }
}
