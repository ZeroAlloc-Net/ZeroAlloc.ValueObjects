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
            .Select(static (m, _) => m!)
            .WithTrackingName(TrackingNames.TypedIdCandidates);

        // Combine with compilation-level [TypedIdDefault] reading
        var assemblyDefault = context.CompilationProvider
            .Select(static (comp, _) => TypedIdParser.ReadAssemblyDefault(comp))
            .WithTrackingName(TrackingNames.TypedIdAssemblyDefault);

        // ZATI009 — the one check that needs every struct at once. Only the hint names that must
        // not be added reach the per-struct outputs, so an edit that leaves them equal keeps
        // every other struct's output cached.
        var collisions = candidates
            .Select(static (m, _) => m.File)
            .Where(static f => f is not null)
            .Select(static (f, _) => f!)
            .Collect()
            .Select(static (files, _) => CaseCollisions.Find(files, TypedIdDiagnostics.NameDiffersOnlyInCase))
            .WithTrackingName(TrackingNames.TypedIdCaseCollisions);

        context.RegisterSourceOutput(collisions, static (ctx, found) =>
        {
            foreach (var diagnostic in found.Diagnostics) ctx.ReportDiagnostic(diagnostic.ToDiagnostic());
        });

        var combined = candidates.Combine(assemblyDefault).Combine(collisions);

        context.RegisterSourceOutput(combined, static (ctx, pair) =>
        {
            var ((partial, asmDefault), found) = pair;

            // Report any diagnostics captured during parsing. A struct without a File is not
            // generated: it has an error, or ZATI006; see TypedIdParser.Parse.
            foreach (var info in partial.Diagnostics)
                ctx.ReportDiagnostic(info.ToDiagnostic());

            if (partial.File is null || found.Skips(partial.HintName)) return;

            var resolved = TypedIdParser.Resolve(partial, asmDefault);
            var source = resolved.Backing switch
            {
                1 => TypedIdGuidWriter.Write(resolved),
                2 => TypedIdInt64Writer.Write(resolved),
                _ => throw new System.InvalidOperationException(
                    $"Unexpected TypedId backing {resolved.Backing} for {resolved.Name}"),
            };
            ctx.AddSource(partial.HintName, source);
        });
    }
}
