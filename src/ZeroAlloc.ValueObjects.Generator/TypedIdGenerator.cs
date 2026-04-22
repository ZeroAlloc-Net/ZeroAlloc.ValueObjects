using Microsoft.CodeAnalysis;
using ZeroAlloc.ValueObjects.Generator.Pipeline;
using ZeroAlloc.ValueObjects.Generator.Writers;

namespace ZeroAlloc.ValueObjects.Generator;

[Generator]
public sealed class TypedIdGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Collect per-struct [TypedId] candidates
        var candidates = context.SyntaxProvider.ForAttributeWithMetadataName(
                "ZeroAlloc.ValueObjects.TypedIdAttribute",
                predicate: static (node, _) =>
                    node is Microsoft.CodeAnalysis.CSharp.Syntax.StructDeclarationSyntax
                    || node is Microsoft.CodeAnalysis.CSharp.Syntax.RecordDeclarationSyntax,
                transform: static (ctx, ct) => TypedIdParser.Parse(ctx, ct))
            .Where(static m => m is not null)
            .Select(static (m, _) => m!);

        // Combine with compilation-level [TypedIdDefault] reading
        var assemblyDefault = context.CompilationProvider
            .Select(static (comp, _) => TypedIdParser.ReadAssemblyDefault(comp));

        var combined = candidates.Combine(assemblyDefault);

        context.RegisterSourceOutput(combined, static (ctx, pair) =>
        {
            var (partial, asmDefault) = pair;
            var resolved = TypedIdParser.Resolve(partial, asmDefault);
            var source = TypedIdStubWriter.Write(resolved);
            var hintName = resolved.Namespace is null
                ? $"{resolved.Name}.TypedId.g.cs"
                : $"{resolved.Namespace}_{resolved.Name}.TypedId.g.cs";
            ctx.AddSource(hintName, source);
        });
    }
}
