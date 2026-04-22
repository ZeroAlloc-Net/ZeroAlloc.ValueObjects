namespace ZeroAlloc.ValueObjects;

/// <summary>
/// Storage representation chosen for a <see cref="TypedIdAttribute"/>-decorated struct.
/// </summary>
public enum BackingType
{
    /// <summary>
    /// Let the generator choose a backing type appropriate for the selected <see cref="IdStrategy"/>.
    /// </summary>
    Default = 0,

    /// <summary>
    /// Use a <see cref="System.Guid"/> as the underlying storage (128-bit).
    /// </summary>
    Guid,

    /// <summary>
    /// Use a <see cref="long"/> as the underlying storage (64-bit).
    /// </summary>
    Int64,
}
