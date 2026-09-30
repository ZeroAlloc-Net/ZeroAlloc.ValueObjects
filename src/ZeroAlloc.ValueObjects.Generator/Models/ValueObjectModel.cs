namespace ZeroAlloc.ValueObjects.Generator.Models;

/// <param name="TypeName">The value object's simple name, as written in text such as ToString.</param>
/// <param name="TypeParameters">Its type parameter list, such as <c>&lt;T&gt;</c>, or empty.</param>
/// <param name="ContainingTypes">
/// The partial declarations of its containing types, outermost first; empty at the top level.
/// </param>
internal sealed record ValueObjectModel(
    string HintName,
    string Namespace,
    string TypeName,
    bool IsStruct,
    EquatableArray<EqualityProperty> Properties,
    string TypeParameters,
    EquatableArray<string> ContainingTypes);
