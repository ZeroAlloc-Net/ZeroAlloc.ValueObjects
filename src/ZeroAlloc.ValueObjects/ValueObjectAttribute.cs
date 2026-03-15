using System;

namespace ZeroAlloc.ValueObjects;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class ValueObjectAttribute : Attribute
{
    /// <summary>
    /// When true, always generates a class even if auto-detection would choose struct.
    /// </summary>
    public bool ForceClass { get; set; }
}
