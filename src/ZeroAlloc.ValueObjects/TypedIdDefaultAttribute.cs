using System;

namespace ZeroAlloc.ValueObjects;

/// <summary>
/// Assembly-level defaults applied to every <see cref="TypedIdAttribute"/>-decorated struct
/// in the current assembly that does not explicitly set <see cref="TypedIdAttribute.Strategy"/>
/// or <see cref="TypedIdAttribute.Backing"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, Inherited = false)]
public sealed class TypedIdDefaultAttribute : Attribute
{
    /// <summary>
    /// Default identifier generation strategy. Defaults to <see cref="IdStrategy.Ulid"/>.
    /// </summary>
    public IdStrategy Strategy { get; init; } = IdStrategy.Ulid;

    /// <summary>
    /// Default backing storage. Defaults to <see cref="BackingType.Default"/>,
    /// i.e. the generator picks per-strategy.
    /// </summary>
    public BackingType Backing { get; init; }
}
