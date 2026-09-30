using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using ZeroAlloc.ValueObjects.Generator;

namespace ZeroAlloc.ValueObjects.Tests.GeneratorTests;

/// <summary>
/// The value object models compare by value, so an edit that does not touch a value object
/// leaves every tracked step and output cached, including the case-collision check.
/// </summary>
public sealed class ValueObjectIncrementalityTests
{
    // ZAVO003 on money, ZAVO001 on Hidden, and a nested and a generic value object.
    private const string ValueObjectsSource = """
        using ZeroAlloc.ValueObjects;
        namespace MyApp;

        [ValueObject] public partial class Money { public int Value { get; } }
        [ValueObject] public partial class money { public int Value { get; } }
        public partial class Orders { [ValueObject] public partial struct Line { public int Qty { get; } } }
        public class Closed { [ValueObject] public partial class Hidden { public int Value { get; } } }
        [ValueObject] public partial class Range<T> { public T? Low { get; } public T? High { get; } }
        """;

    private static readonly string[] GeneratorStepNames = ["ValueObjectTargets", "ValueObjectCaseCollisions"];

    [Fact]
    public void UnrelatedEdit_LeavesEveryTrackedStepAndOutputCached()
    {
        var values = CSharpSyntaxTree.ParseText(ValueObjectsSource, path: "/src/Values.cs");
        var unrelated = CSharpSyntaxTree.ParseText(
            "namespace MyApp; public class Unrelated { public int M() => 1; }", path: "/src/Unrelated.cs");
        var compilation = TypedIdDiagnosticTests.CreateCompilation(values, unrelated);

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new ValueObjectGenerator().AsSourceGenerator()],
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));

        driver = driver.RunGenerators(compilation);
        var first = driver.GetRunResult().Results[0];

        var edited = compilation.ReplaceSyntaxTree(
            unrelated,
            unrelated.WithChangedText(SourceText.From(
                "namespace MyApp; public class Unrelated { public int M() => 2; public int N() => 3; }")));
        driver = driver.RunGenerators(edited);
        var second = driver.GetRunResult().Results[0];

        foreach (var name in GeneratorStepNames)
        {
            Assert.True(second.TrackedSteps.TryGetValue(name, out var runSteps), $"Step '{name}' was not tracked.");
            AssertAllCachedOrUnchanged(name, runSteps);
        }

        Assert.NotEmpty(second.TrackedOutputSteps);
        foreach (var step in second.TrackedOutputSteps)
            AssertAllCachedOrUnchanged(step.Key, step.Value);

        Assert.Equal(
            ["ZAVO001", "ZAVO003"],
            second.Diagnostics.Select(d => d.Id).OrderBy(i => i, StringComparer.Ordinal),
            StringComparer.Ordinal);
        Assert.Equal(first.GeneratedSources.Length, second.GeneratedSources.Length);
        Assert.Equal(3, second.GeneratedSources.Length);
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
}
