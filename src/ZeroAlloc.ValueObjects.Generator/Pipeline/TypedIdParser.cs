using System.Threading;
using Microsoft.CodeAnalysis;
using ZeroAlloc.ValueObjects.Generator.Models;

namespace ZeroAlloc.ValueObjects.Generator.Pipeline;

internal static class TypedIdParser
{
    // "Partial" model: values as seen on the struct's [TypedId] attribute (may be 0 = unset).
    // Full resolution happens in Resolve() combining with the assembly-level default.
    internal sealed record PartialModel(string? Namespace, string Name, int RawStrategy, int RawBacking);

    internal sealed record AssemblyDefault(int RawStrategy, int RawBacking);

    public static PartialModel? Parse(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
    {
        if (ctx.TargetSymbol is not INamedTypeSymbol symbol) return null;
        var attr = ctx.Attributes[0];

        int strategy = ReadNamedInt(attr, "Strategy", -1);
        int backing = ReadNamedInt(attr, "Backing", 0);

        var ns = symbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : symbol.ContainingNamespace.ToDisplayString();

        return new PartialModel(ns, symbol.Name, strategy, backing);
    }

    public static AssemblyDefault ReadAssemblyDefault(Compilation compilation)
    {
        var defaultAttrSymbol = compilation.GetTypeByMetadataName("ZeroAlloc.ValueObjects.TypedIdDefaultAttribute");
        if (defaultAttrSymbol is null) return new AssemblyDefault(-1, 0);

        foreach (var attr in compilation.Assembly.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, defaultAttrSymbol))
            {
                int strategy = ReadNamedInt(attr, "Strategy", -1);
                int backing = ReadNamedInt(attr, "Backing", 0);
                return new AssemblyDefault(strategy, backing);
            }
        }

        return new AssemblyDefault(-1, 0);
    }

    public static TypedIdModel Resolve(PartialModel partial, AssemblyDefault asmDefault)
    {
        // Strategy: per-struct → assembly default → Ulid (0)
        int strategy = partial.RawStrategy >= 0
            ? partial.RawStrategy
            : (asmDefault.RawStrategy >= 0 ? asmDefault.RawStrategy : 0);

        // Backing: per-struct → assembly default → auto (1=Guid for Ulid/Uuid7, 2=Int64 for Snowflake/Sequential)
        int backing = partial.RawBacking > 0
            ? partial.RawBacking
            : (asmDefault.RawBacking > 0 ? asmDefault.RawBacking : AutoBacking(strategy));

        return new TypedIdModel(
            Namespace: partial.Namespace,
            Name: partial.Name,
            Strategy: strategy,
            Backing: backing);
    }

    private static int AutoBacking(int strategy) => strategy switch
    {
        0 or 1 => 1,   // Ulid, Uuid7 → Guid
        2 or 3 => 2,   // Snowflake, Sequential → Int64
        _ => 1,
    };

    private static int ReadNamedInt(AttributeData attr, string name, int defaultValue)
    {
        foreach (var kv in attr.NamedArguments)
        {
            if (string.Equals(kv.Key, name, System.StringComparison.Ordinal) && kv.Value.Value is int v)
                return v;
        }
        return defaultValue;
    }
}
