using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ZeroAlloc.ValueObjects.Generator;

namespace ZeroAlloc.ValueObjects.Tests.GeneratorTests;

public sealed class TypedIdDiagnosticTests
{
    [Fact]
    public void ZATI001_SnowflakeWithGuidBacking_ProducesError()
    {
        var diagnostics = GetDiagnostics(
            """
            using ZeroAlloc.ValueObjects;
            [TypedId(Strategy = IdStrategy.Snowflake, Backing = BackingType.Guid)]
            public readonly partial record struct Bad;
            """);
        Assert.Contains(diagnostics, d => string.Equals(d.Id, "ZATI001", StringComparison.Ordinal) && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void ZATI001_UlidWithInt64Backing_ProducesError()
    {
        var diagnostics = GetDiagnostics(
            """
            using ZeroAlloc.ValueObjects;
            [TypedId(Strategy = IdStrategy.Ulid, Backing = BackingType.Int64)]
            public readonly partial record struct Bad;
            """);
        Assert.Contains(diagnostics, d => string.Equals(d.Id, "ZATI001", StringComparison.Ordinal) && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void ZATI002_NotReadonlyPartialRecordStruct_ProducesError()
    {
        // Missing 'readonly'
        var diagnostics = GetDiagnostics(
            """
            using ZeroAlloc.ValueObjects;
            [TypedId]
            public partial record struct Mutable;
            """);
        Assert.Contains(diagnostics, d => string.Equals(d.Id, "ZATI002", StringComparison.Ordinal) && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void ZATI003_BodyHasField_ProducesError()
    {
        var diagnostics = GetDiagnostics(
            """
            using ZeroAlloc.ValueObjects;
            [TypedId]
            public readonly partial record struct Bad { public int Extra; }
            """);
        Assert.Contains(diagnostics, d => string.Equals(d.Id, "ZATI003", StringComparison.Ordinal) && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void ZATI005_MultiFilePartial_ProducesWarning()
    {
        var diagnostics = GetDiagnostics(
            """
            using ZeroAlloc.ValueObjects;
            [TypedId]
            public readonly partial record struct Split;
            """,
            """
            public readonly partial record struct Split;
            """);
        Assert.Contains(diagnostics, d => string.Equals(d.Id, "ZATI005", StringComparison.Ordinal) && d.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public void ValidDeclaration_ProducesNoZATIDiagnostics()
    {
        var diagnostics = GetDiagnostics(
            """
            using ZeroAlloc.ValueObjects;
            [TypedId(Strategy = IdStrategy.Ulid)]
            public readonly partial record struct Good;
            """);
        Assert.DoesNotContain(diagnostics, d => d.Id.StartsWith("ZATI", StringComparison.Ordinal));
    }

    internal static ImmutableArray<Diagnostic> GetDiagnostics(params string[] sources) =>
        GetDiagnostics(severities: null, sources);

    /// <summary>
    /// Runs the generator and returns its diagnostics as the driver filters them: a
    /// <c>#pragma warning disable</c> marks the ones it covers as suppressed.
    /// </summary>
    internal static ImmutableArray<Diagnostic> GetDiagnostics(
        IReadOnlyDictionary<string, ReportDiagnostic>? severities, params string[] sources)
    {
        var trees = new SyntaxTree[sources.Length];
        for (int i = 0; i < sources.Length; i++)
        {
            // Give each tree a distinct file path so multi-file partial detection (ZATI005) works.
            trees[i] = CSharpSyntaxTree.ParseText(sources[i], path: FilePath(i));
        }

        var compilation = CreateCompilation(trees);
        if (severities is not null)
            compilation = compilation.WithOptions(compilation.Options.WithSpecificDiagnosticOptions(severities));

        var driver = CSharpGeneratorDriver.Create(new TypedIdGenerator());
        var result = driver.RunGenerators(compilation).GetRunResult();
        return result.Diagnostics;
    }

    /// <summary>The file path that GetDiagnostics gives the source at <paramref name="index"/>.</summary>
    internal static string FilePath(int index) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"source{index}.cs");

    internal static CSharpCompilation CreateCompilation(params SyntaxTree[] trees)
    {
        var runtimeDir = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();
        var refs = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(TypedIdAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
            MetadataReference.CreateFromFile(System.IO.Path.Combine(runtimeDir, "System.Runtime.dll")),
            MetadataReference.CreateFromFile(System.IO.Path.Combine(runtimeDir, "netstandard.dll")),
        };
        return CSharpCompilation.Create(
            "GenTest",
            trees,
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
