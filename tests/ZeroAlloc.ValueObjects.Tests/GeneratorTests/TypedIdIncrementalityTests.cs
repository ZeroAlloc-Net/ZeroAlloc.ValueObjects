using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using ZeroAlloc.ValueObjects.Generator;

namespace ZeroAlloc.ValueObjects.Tests.GeneratorTests;

/// <summary>
/// The cached TypedId models carry source locations, so an edit that does not touch a TypedId
/// must leave every tracked step and output cached, and an edit that moves one must move its
/// diagnostic.
/// </summary>
public sealed class TypedIdIncrementalityTests
{
    // ZATI001 on Bad, ZATI003 on WithBody, and one clean TypedId.
    private const string IdsSource = """
        using ZeroAlloc.ValueObjects;
        namespace MyApp;

        [TypedId(Strategy = IdStrategy.Snowflake, Backing = BackingType.Guid)]
        public readonly partial record struct Bad;

        [TypedId]
        public readonly partial record struct WithBody { public int Extra; }

        [TypedId(Strategy = IdStrategy.Ulid)]
        public readonly partial record struct Good;
        """;

    // The tracking names the generator gives its steps, in ZeroAlloc.ValueObjects.Generator.TrackingNames.
    private static readonly string[] GeneratorStepNames = ["TypedIdCandidates", "TypedIdAssemblyDefault"];

    [Fact]
    public void UnrelatedEdit_LeavesEveryTrackedStepAndOutputCached()
    {
        var ids = CSharpSyntaxTree.ParseText(IdsSource, path: "/src/Ids.cs");
        var unrelated = CSharpSyntaxTree.ParseText(
            "namespace MyApp; public class Unrelated { public int M() => 1; }", path: "/src/Unrelated.cs");
        var compilation = TypedIdDiagnosticTests.CreateCompilation(ids, unrelated);

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new TypedIdGenerator().AsSourceGenerator()],
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));

        driver = driver.RunGenerators(compilation);
        var first = driver.GetRunResult().Results[0];

        var edited = compilation.ReplaceSyntaxTree(
            unrelated,
            unrelated.WithChangedText(SourceText.From(
                "namespace MyApp; public class Unrelated { public int M() => 2; public int N() => 3; }")));
        driver = driver.RunGenerators(edited);
        var second = driver.GetRunResult().Results[0];

        // Roslyn's own steps rerun on every edit; the generator's named steps must not change.
        foreach (var name in GeneratorStepNames)
        {
            Assert.True(second.TrackedSteps.TryGetValue(name, out var runSteps), $"Step '{name}' was not tracked.");
            AssertAllCachedOrUnchanged(name, runSteps);
        }

        Assert.NotEmpty(second.TrackedOutputSteps);
        foreach (var step in second.TrackedOutputSteps)
            AssertAllCachedOrUnchanged(step.Key, step.Value);

        // A cached output still reports its diagnostics at the same place, bound to the tree.
        Assert.Equal(Describe(first.Diagnostics), Describe(second.Diagnostics));
        Assert.Equal(
            ["ZATI001", "ZATI003"],
            second.Diagnostics.Select(d => d.Id).OrderBy(i => i, StringComparer.Ordinal),
            StringComparer.Ordinal);
        Assert.All(second.Diagnostics, d => Assert.Same(ids, d.Location.SourceTree));
    }

    [Fact]
    public void EditAboveATypedId_MovesItsDiagnostic()
    {
        var ids = CSharpSyntaxTree.ParseText(IdsSource, path: "/src/Ids.cs");
        var compilation = TypedIdDiagnosticTests.CreateCompilation(ids);

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new TypedIdGenerator());
        driver = driver.RunGenerators(compilation);
        var before = AssertEx.One(driver.GetRunResult().Diagnostics, d => string.Equals(d.Id, "ZATI003", StringComparison.Ordinal));

        var moved = ids.WithChangedText(SourceText.From(
            IdsSource.Replace("namespace MyApp;", "namespace MyApp;\n\n// two\n// more lines", StringComparison.Ordinal)));
        driver = driver.RunGenerators(compilation.ReplaceSyntaxTree(ids, moved));
        var after = AssertEx.One(driver.GetRunResult().Diagnostics, d => string.Equals(d.Id, "ZATI003", StringComparison.Ordinal));

        Assert.Equal(
            before.Location.GetLineSpan().StartLinePosition.Line + 3,
            after.Location.GetLineSpan().StartLinePosition.Line);
        Assert.Same(moved, after.Location.SourceTree);
    }

    private static void AssertAllCachedOrUnchanged(string stepName, ImmutableArray<IncrementalGeneratorRunStep> runSteps)
    {
        foreach (var runStep in runSteps)
        {
            foreach (var (_, reason) in runStep.Outputs)
            {
                Assert.True(
                    reason == IncrementalStepRunReason.Cached || reason == IncrementalStepRunReason.Unchanged,
                    $"Step '{stepName}' produced output with reason {reason} after an unrelated edit.");
            }
        }
    }

    private static List<string> Describe(ImmutableArray<Diagnostic> diagnostics) =>
        diagnostics.Select(d => $"{d.Id} {d.Location.GetLineSpan()}").OrderBy(s => s, StringComparer.Ordinal).ToList();
}
