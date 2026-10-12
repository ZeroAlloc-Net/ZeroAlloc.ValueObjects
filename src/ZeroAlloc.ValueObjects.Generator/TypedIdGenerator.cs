using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using ZeroAlloc.ValueObjects.Generator.Models;
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

        // The EF Core registration is generated only for a compilation that can compile it: one
        // that references ZeroAlloc.ValueObjects.EfCore, and through it EF Core.
        var efCore = context.CompilationProvider
            .Select(static (comp, _) => ReferencesEfCore(comp))
            .WithTrackingName(TrackingNames.TypedIdEfCore);

        RegisterTypedIds(context, candidates, assemblyDefault, collisions, efCore);
        RegisterEfCoreConventions(context, candidates, collisions, efCore);
    }

    private static void RegisterTypedIds(
        IncrementalGeneratorInitializationContext context,
        IncrementalValuesProvider<TypedIdParser.PartialModel> candidates,
        IncrementalValueProvider<TypedIdParser.AssemblyDefault> assemblyDefault,
        IncrementalValueProvider<CaseCollisions> collisions,
        IncrementalValueProvider<bool> efCore)
    {
        var combined = candidates.Combine(assemblyDefault).Combine(collisions).Combine(efCore);

        context.RegisterSourceOutput(combined, static (ctx, pair) =>
        {
            var (((partial, asmDefault), found), hasEfCore) = pair;

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

            if (hasEfCore)
                ctx.AddSource(TypedIdEfCoreWriter.MemberHintName(partial.HintName), TypedIdEfCoreWriter.WriteMember(resolved));
        });
    }

    // Add{Assembly}TypedIdConventions, which calls AddTypedIdConvention on every generated struct
    // that namespace-level code in the assembly can name.
    private static void RegisterEfCoreConventions(
        IncrementalGeneratorInitializationContext context,
        IncrementalValuesProvider<TypedIdParser.PartialModel> candidates,
        IncrementalValueProvider<CaseCollisions> collisions,
        IncrementalValueProvider<bool> efCore)
    {

        // The org-wide ZeroAllocGeneratedAccessibility property sets the accessibility of the EF
        // Core conventions class, the one entry point generated outside the user's own types.
        var accessibility = context.AnalyzerConfigOptionsProvider
            .Select(static (options, _) => GeneratedAccessibility.Resolve(options))
            .WithTrackingName(TrackingNames.TypedIdGeneratedAccessibility);

        context.RegisterSourceOutput(accessibility, static (ctx, option) =>
        {
            if (option.InvalidValue is not null)
                ctx.ReportDiagnostic(Diagnostic.Create(TypedIdDiagnostics.InvalidGeneratedAccessibility, Location.None, option.InvalidValue));
        });

        var entries = candidates
            .Where(static m => m.File is not null)
            .Select(static (m, _) => new TypedIdConventionEntry(m.HintName, m.FullyQualifiedName, m.ReachableFromAssembly))
            .Collect()
            .Select(static (all, _) => new EquatableArray<TypedIdConventionEntry>(all))
            .WithTrackingName(TrackingNames.TypedIdConventionEntries);

        var assemblyName = context.CompilationProvider
            .Select(static (comp, _) => comp.AssemblyName ?? "Assembly");

        var conventions = entries.Combine(collisions).Combine(efCore).Combine(assemblyName).Combine(accessibility);

        context.RegisterSourceOutput(conventions, static (ctx, pair) =>
        {
            var ((((all, found), hasEfCore), asmName), option) = pair;
            if (!hasEfCore) return;

            var names = new List<string>();
            var any = false;
            foreach (var entry in all)
            {
                if (found.Skips(entry.HintName)) continue;
                any = true;
                if (entry.ReachableFromAssembly) names.Add(entry.FullyQualifiedName);
            }

            // A compilation without a generated [TypedId] gets no conventions class.
            if (!any) return;

            names.Sort(System.StringComparer.Ordinal);
            ctx.AddSource(TypedIdEfCoreWriter.ConventionsHintName, TypedIdEfCoreWriter.WriteConventions(asmName, option.Keyword, names));
        });
    }

    private static bool ReferencesEfCore(Compilation compilation) =>
        compilation.GetTypeByMetadataName("ZeroAlloc.ValueObjects.EfCore.TypedIdValueConverter`2") is not null &&
        compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.ModelConfigurationBuilder") is not null;
}
