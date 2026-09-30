using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ZeroAlloc.ValueObjects.Tests.GeneratorTests;

/// <summary>
/// Runs a generator over source and compiles the result against the real runtime, System.Text.Json
/// and ZeroAlloc.Serialisation, so a test can check the generated code builds, not just that it
/// was produced.
/// </summary>
internal sealed record GeneratorRun(
    ImmutableArray<Diagnostic> GeneratorDiagnostics,
    IReadOnlyList<string> HintNames,
    IReadOnlyList<Diagnostic> Errors,
    IReadOnlyList<Diagnostic> Warnings,
    Compilation Output)
{
    private static readonly CSharpParseOptions ParseOptions =
        new CSharpParseOptions(LanguageVersion.Latest).WithPreprocessorSymbols("NET");

    public static GeneratorRun Of(IIncrementalGenerator generator, params string[] sources)
    {
        var trees = sources
            .Select((s, i) => CSharpSyntaxTree.ParseText(s, ParseOptions, path: $"source{i}.cs"))
            .ToArray();
        var compilation = CSharpCompilation.Create(
            "GenTest",
            trees,
            References(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var driver = CSharpGeneratorDriver.Create([generator.AsSourceGenerator()], parseOptions: ParseOptions)
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        var result = driver.GetRunResult().Results[0];

        var all = output.GetDiagnostics();
        return new GeneratorRun(
            result.Diagnostics,
            result.GeneratedSources.Select(s => s.HintName).OrderBy(n => n, StringComparer.Ordinal).ToArray(),
            all.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray(),
            all.Where(d => d.Severity == DiagnosticSeverity.Warning).ToArray(),
            output);
    }

    private static MetadataReference[] References()
    {
        var trusted = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(System.IO.Path.PathSeparator)
            .Where(p => !string.IsNullOrEmpty(p));
        var extra = new[]
        {
            typeof(TypedIdAttribute).Assembly.Location,
            typeof(ZeroAlloc.Serialisation.ISerializer<>).Assembly.Location,
        };
        return trusted.Concat(extra)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .ToArray();
    }
}
