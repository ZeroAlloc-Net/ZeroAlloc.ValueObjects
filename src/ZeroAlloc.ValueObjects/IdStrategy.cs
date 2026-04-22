namespace ZeroAlloc.ValueObjects;

/// <summary>
/// Identifier generation strategy for a <see cref="TypedIdAttribute"/>-decorated struct.
/// </summary>
public enum IdStrategy
{
    /// <summary>
    /// Lexicographically sortable 128-bit identifier (ULID). Default strategy.
    /// </summary>
    Ulid,

    /// <summary>
    /// Time-ordered UUID version 7 (RFC 9562).
    /// </summary>
    Uuid7,

    /// <summary>
    /// Twitter-style 64-bit snowflake identifier composed of timestamp, machine, and sequence.
    /// </summary>
    Snowflake,

    /// <summary>
    /// Monotonically increasing sequential identifier.
    /// </summary>
    Sequential,
}
