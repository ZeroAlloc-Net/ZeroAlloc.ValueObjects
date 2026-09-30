using Microsoft.CodeAnalysis;
using ZeroAlloc.ValueObjects.Generator.Models;
using ZeroAlloc.ValueObjects.Generator.Pipeline;
using ZeroAlloc.ValueObjects.Generator.Writers;

namespace ZeroAlloc.ValueObjects.Generator;

[Generator]
public sealed class ValueObjectGenerator : IIncrementalGenerator
{
    private const string ValueObjectAttributeFqn = "ZeroAlloc.ValueObjects.ValueObjectAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var targets = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                ValueObjectAttributeFqn,
                predicate: (node, _) => ValueObjectParser.IsValueObjectCandidate(node),
                transform: ValueObjectParser.Parse)
            .Where(m => m is not null)
            .Select((m, _) => m!)
            .WithTrackingName(TrackingNames.ValueObjectTargets);

        // ZAVO003 — the one check that needs every value object at once. Only the hint names that
        // must not be added reach the per-type outputs, so an edit that leaves them equal keeps
        // every other value object's output cached.
        var collisions = targets
            .Select(static (t, _) => t.File)
            .Where(static f => f is not null)
            .Select(static (f, _) => f!)
            .Collect()
            .Select(static (files, _) => CaseCollisions.Find(files, ValueObjectDiagnostics.NameDiffersOnlyInCase))
            .WithTrackingName(TrackingNames.ValueObjectCaseCollisions);

        context.RegisterSourceOutput(collisions, static (ctx, found) =>
        {
            foreach (var diagnostic in found.Diagnostics) ctx.ReportDiagnostic(diagnostic.ToDiagnostic());
        });

        context.RegisterSourceOutput(targets.Combine(collisions), static (ctx, pair) =>
        {
            var (target, found) = pair;
            foreach (var diagnostic in target.Diagnostics) ctx.ReportDiagnostic(diagnostic.ToDiagnostic());
            if (target.Model is { } model && !found.Skips(model.HintName))
                ctx.AddSource(model.HintName, SourceWriter.Write(model));
        });
    }
}
