namespace ZeroAlloc.ValueObjects.Generator.Models;

/// <param name="IsTypeParameter">
/// The property's type is a type parameter, or a nullable one. <c>==</c> is not defined for an
/// unconstrained type parameter, so such a member compares through
/// <c>EqualityComparer&lt;T&gt;.Default</c>.
/// </param>
internal sealed record EqualityProperty(string Name, string TypeName, bool IsNullable, bool IsTypeParameter);
