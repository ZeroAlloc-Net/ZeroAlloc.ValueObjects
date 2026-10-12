using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ZeroAlloc.ValueObjects.Tests.GeneratorTests;

/// <summary>
/// Runs a generator over source and compiles the result against the real runtime, System.Text.Json
/// and ZeroAlloc.Serialisation, so a test can check the generated code builds, not just that it
/// was produced. EF Core and ZeroAlloc.ValueObjects.EfCore are referenced only by
/// <see cref="OfWithEfCore"/>, since the generator adds the EF Core registration for a compilation that
/// references them.
/// </summary>
internal sealed record GeneratorRun(
    ImmutableArray<Diagnostic> GeneratorDiagnostics,
    IReadOnlyList<string> HintNames,
    IReadOnlyList<Diagnostic> Errors,
    IReadOnlyList<Diagnostic> Warnings,
    Compilation Output,
    GeneratorDriver Driver)
{
    private static readonly CSharpParseOptions ParseOptions =
        new CSharpParseOptions(LanguageVersion.Latest).WithPreprocessorSymbols("NET");

    public static GeneratorRun Of(IIncrementalGenerator generator, params string[] sources) =>
        Run(generator, efCore: false, globalOptions: null, sources);

    /// <summary>As <see cref="Of"/>, with EF Core and ZeroAlloc.ValueObjects.EfCore referenced.</summary>
    public static GeneratorRun OfWithEfCore(IIncrementalGenerator generator, params string[] sources) =>
        Run(generator, efCore: true, globalOptions: null, sources);

    /// <summary>
    /// As <see cref="OfWithEfCore"/>, with MSBuild properties the generator reads as global options,
    /// such as <c>build_property.ZeroAllocGeneratedAccessibility</c>.
    /// </summary>
    public static GeneratorRun OfWithEfCore(
        IIncrementalGenerator generator, IReadOnlyDictionary<string, string> globalOptions, params string[] sources) =>
        Run(generator, efCore: true, globalOptions, sources);

    private static GeneratorRun Run(
        IIncrementalGenerator generator, bool efCore, IReadOnlyDictionary<string, string>? globalOptions, string[] sources)
    {
        var trees = sources
            .Select((s, i) => CSharpSyntaxTree.ParseText(s, ParseOptions, path: $"source{i}.cs"))
            .ToArray();
        var compilation = CSharpCompilation.Create(
            "GenTest",
            trees,
            References(efCore),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var driver = CSharpGeneratorDriver.Create(
                [generator.AsSourceGenerator()],
                parseOptions: ParseOptions,
                optionsProvider: globalOptions is null ? null : new GlobalOptionsProvider(globalOptions))
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        var result = driver.GetRunResult().Results[0];

        var all = output.GetDiagnostics();
        return new GeneratorRun(
            result.Diagnostics,
            result.GeneratedSources.Select(s => s.HintName).OrderBy(n => n, StringComparer.Ordinal).ToArray(),
            all.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray(),
            all.Where(d => d.Severity == DiagnosticSeverity.Warning).ToArray(),
            output,
            driver);
    }

    private static MetadataReference[] References(bool efCore)
    {
        // The test host lists every dependency of the test project as trusted, EF Core included.
        var trusted = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(System.IO.Path.PathSeparator)
            .Where(p => !string.IsNullOrEmpty(p))
            .Where(p => efCore || !IsEfCore(System.IO.Path.GetFileName(p)));
        var extra = new[]
        {
            typeof(TypedIdAttribute).Assembly.Location,
            typeof(ZeroAlloc.Serialisation.ISerializer<>).Assembly.Location,
        };
        if (efCore)
        {
            extra =
            [
                .. extra,
                typeof(ZeroAlloc.ValueObjects.EfCore.TypedIdValueConverter<,>).Assembly.Location,
                typeof(Microsoft.EntityFrameworkCore.ModelConfigurationBuilder).Assembly.Location,
            ];
        }
        return trusted.Concat(extra)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .ToArray();
    }

    private static bool IsEfCore(string fileName) =>
        fileName.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.OrdinalIgnoreCase) ||
        fileName.StartsWith("ZeroAlloc.ValueObjects.EfCore", StringComparison.OrdinalIgnoreCase);

    private sealed class GlobalOptionsProvider(IReadOnlyDictionary<string, string> values) : AnalyzerConfigOptionsProvider
    {
        private readonly Options _global = new(values);
        private static readonly Options s_empty = new(new Dictionary<string, string>(StringComparer.Ordinal));

        public override AnalyzerConfigOptions GlobalOptions => _global;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => s_empty;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => s_empty;
    }

    private sealed class Options(IReadOnlyDictionary<string, string> values) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? value) =>
            values.TryGetValue(key, out value);
    }
}
