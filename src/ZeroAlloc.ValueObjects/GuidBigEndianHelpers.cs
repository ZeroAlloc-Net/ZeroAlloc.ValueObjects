using System;

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
/// layout used by ULID and RFC 9562 UUIDv7. This helper reads and writes through the
/// <c>bigEndian</c> overloads of <see cref="Guid"/>, so callers can treat the 16-byte view
/// as big-endian unconditionally.
/// </remarks>
internal static class GuidBigEndianHelpers
{
    /// <summary>
    /// Converts a 16-byte big-endian buffer into a <see cref="Guid"/> such that
    /// <see cref="GuidToBigEndianBytes"/> returns the original byte sequence.
    /// </summary>
    /// <param name="be">A 16-byte big-endian input buffer.</param>
    /// <returns>A <see cref="Guid"/> whose big-endian view equals <paramref name="be"/>.</returns>
    public static Guid BigEndianBytesToGuid(ReadOnlySpan<byte> be) => new(be, bigEndian: true);

    /// <summary>
    /// Writes the big-endian 16-byte representation of <paramref name="g"/> into
    /// <paramref name="dest"/>, which must be at least 16 bytes long. Inverse of
    /// <see cref="BigEndianBytesToGuid"/>.
    /// </summary>
    /// <param name="g">The value to serialise.</param>
    /// <param name="dest">The destination buffer (at least 16 bytes).</param>
    public static void GuidToBigEndianBytes(Guid g, Span<byte> dest)
    {
        if (!g.TryWriteBytes(dest, bigEndian: true, out _))
            throw new ArgumentOutOfRangeException(nameof(dest), "The destination buffer must be at least 16 bytes long.");
    }
}
