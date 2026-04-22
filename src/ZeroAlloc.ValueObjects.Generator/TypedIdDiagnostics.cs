using Microsoft.CodeAnalysis;

namespace ZeroAlloc.ValueObjects.Generator;

internal static class TypedIdDiagnostics
{
    private const string Category = "ZeroAlloc.ValueObjects.TypedId";

    public static readonly DiagnosticDescriptor IncompatibleBacking = new(
        id: "ZATI001",
        title: "Incompatible strategy/backing combination",
        messageFormat: "[TypedId] strategy '{0}' requires {1} backing; '{2}' is incompatible",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidDeclaration = new(
        id: "ZATI002",
        title: "[TypedId] requires readonly partial record struct",
        messageFormat: "[TypedId] struct '{0}' must be declared as 'readonly partial record struct'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NonEmptyBody = new(
        id: "ZATI003",
        title: "[TypedId] struct body must be empty",
        messageFormat: "[TypedId] struct '{0}' must have an empty body; the generator owns the 'Value' field",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    // ZATI004 (TypedIdDefault on non-assembly target) is intentionally not emitted by
    // this generator: the compiler already rejects misuse at compile time via
    // [AttributeUsage(AttributeTargets.Assembly)] on TypedIdDefaultAttribute (CS0592).
    // The ID is reserved so future diagnostics don't accidentally reuse it.

    public static readonly DiagnosticDescriptor MultiFilePartial = new(
        id: "ZATI005",
        title: "[TypedId] struct declared across multiple files",
        messageFormat: "[TypedId] struct '{0}' is declared partial in multiple files; generator output may drift — consolidate into one declaration file",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}
