using System.Collections.Generic;

namespace ZeroAlloc.ValueObjects.Generator.Models;

internal sealed record ValueObjectModel(
    string Namespace,
    string TypeName,
    bool IsStruct,
    IReadOnlyList<EqualityProperty> Properties);

internal sealed record EqualityProperty(string Name, string TypeName, bool IsNullable);
