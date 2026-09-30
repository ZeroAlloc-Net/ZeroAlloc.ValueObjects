using System.Collections.Generic;

namespace ZeroAlloc.ValueObjects.Generator.Models;

internal sealed record ValueObjectModel(
    string HintName,
    string Namespace,
    string TypeName,
    bool IsStruct,
    IReadOnlyList<EqualityProperty> Properties);
