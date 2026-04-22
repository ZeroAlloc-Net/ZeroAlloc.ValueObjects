using System;

namespace ZeroAlloc.ValueObjects;

/// <summary>
/// Marks a partial struct as a strongly-typed identifier. The source generator produces
/// equality, parsing, formatting, and factory members using the configured
/// <see cref="Strategy"/> and <see cref="Backing"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Struct, Inherited = false)]
public sealed class TypedIdAttribute : Attribute
{
    /// <summary>
    /// Identifier generation strategy. Defaults to <see cref="IdStrategy.Ulid"/>.
    /// </summary>
    public IdStrategy Strategy { get; init; }

    /// <summary>
    /// Underlying storage type. When <see cref="BackingType.Default"/>, the generator
    /// selects a backing appropriate for <see cref="Strategy"/>.
    /// </summary>
    public BackingType Backing { get; init; }
}
