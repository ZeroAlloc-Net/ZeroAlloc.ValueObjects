using Microsoft.CodeAnalysis;

namespace ZeroAlloc.ValueObjects.Generator;

internal static class ValueObjectDiagnostics
{
    private const string Category = "ZeroAlloc.ValueObjects";

    public static readonly DiagnosticDescriptor ContainingTypeNotPartial = new(
        id: "ZAVO001",
        title: "Nested value object inside a containing type that is not partial",
        messageFormat: "Value object '{0}' is not generated because its containing type '{1}' is not partial",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor FileLocal = new(
        id: "ZAVO002",
        title: "File-local value object",
        messageFormat: "Value object '{0}' is not generated because '{1}' is file-local",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NameDiffersOnlyInCase = new(
        id: "ZAVO003",
        title: "Value object name differs only in case from another value object",
        messageFormat: "Value object '{0}' is not generated because its file name '{1}' differs only in case from that of '{2}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
