using System;

namespace ZeroAlloc.ValueObjects;

/// <summary>
/// Names the static factory that builds a <see cref="ValueObjectAttribute"/> type from its
/// underlying value when the value comes from outside the type, for example from a serializer.
/// </summary>
/// <remarks>
/// The named method must be static, accessible to the consumer, and have one of two shapes:
/// <c>static bool TryCreate(TUnderlying, out T)</c> or <c>static T Create(TUnderlying)</c>.
/// Without this attribute, consumers such as ZeroAlloc.Serialisation fall back to a convention:
/// a method called <c>TryCreate</c> is preferred over one called <c>Create</c>.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class ValueObjectFactoryAttribute : Attribute
{
    /// <summary>Creates the attribute.</summary>
    /// <param name="methodName">Name of the static factory method, usually written with <c>nameof</c>.</param>
    public ValueObjectFactoryAttribute(string methodName) => MethodName = methodName;

    /// <summary>Name of the static factory method.</summary>
    public string MethodName { get; }
}
