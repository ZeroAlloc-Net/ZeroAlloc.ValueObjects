using System;
using System.Runtime.InteropServices;

namespace ZeroAlloc.ValueObjects;

/// <summary>
/// Endian-aware converters between 16-byte big-endian buffers and <see cref="Guid"/>
/// values. Shared by <see cref="UlidCore"/> and <see cref="Uuid7Core"/> so that both
/// strategies present their bytes in the canonical big-endian layout (byte 0 = most
/// significant timestamp byte) regardless of the host's native endianness.
/// </summary>
/// <remarks>
/// .NET's <see cref="Guid"/> stores its first three fields (Data1/Data2/Data3) in
/// native endianness, which means <see cref="Guid.ToByteArray()"/> on a little-endian
/// host emits those fields byte-reversed relative to the standard network-order
/// layout used by ULID and RFC 9562 UUIDv7. This helper performs the necessary swaps
/// so callers can treat the 16-byte view as big-endian unconditionally.
/// </remarks>
internal static class GuidBigEndianHelpers
{
    /// <summary>
    /// Converts a 16-byte big-endian buffer into a <see cref="Guid"/> such that
    /// <see cref="GuidToBigEndianBytes"/> returns the original byte sequence.
    /// </summary>
    /// <param name="be">A 16-byte big-endian input buffer.</param>
    /// <returns>A <see cref="Guid"/> whose big-endian view equals <paramref name="be"/>.</returns>
    public static Guid BigEndianBytesToGuid(ReadOnlySpan<byte> be)
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
    /// Writes the big-endian 16-byte representation of <paramref name="g"/> into
    /// <paramref name="dest"/>, which must be at least 16 bytes long. Inverse of
    /// <see cref="BigEndianBytesToGuid"/>.
    /// </summary>
    /// <param name="g">The value to serialise.</param>
    /// <param name="dest">The destination buffer (at least 16 bytes).</param>
    public static void GuidToBigEndianBytes(Guid g, Span<byte> dest)
    {
        // MemoryMarshal.Write's second parameter is `ref T` on netstandard2.0
        // but `in T` on net7+ (CS9191). Use `in g` so the call is valid against
        // both signatures across our multi-target build.
#if NET7_0_OR_GREATER
        MemoryMarshal.Write(dest, in g);
#else
        MemoryMarshal.Write(dest, ref g);
#endif

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
