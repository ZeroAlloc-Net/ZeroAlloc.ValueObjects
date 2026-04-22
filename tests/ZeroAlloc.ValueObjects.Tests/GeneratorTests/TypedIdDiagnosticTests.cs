using System;
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

    private static ImmutableArray<Diagnostic> GetDiagnostics(params string[] sources)
    {
        var trees = new SyntaxTree[sources.Length];
        for (int i = 0; i < sources.Length; i++)
        {
            // Give each tree a distinct file path so multi-file partial detection (ZATI005) works.
            trees[i] = CSharpSyntaxTree.ParseText(
                sources[i],
                path: string.Create(System.Globalization.CultureInfo.InvariantCulture, $"source{i}.cs"));
        }

        var runtimeDir = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();
        var refs = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(TypedIdAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
            MetadataReference.CreateFromFile(System.IO.Path.Combine(runtimeDir, "System.Runtime.dll")),
            MetadataReference.CreateFromFile(System.IO.Path.Combine(runtimeDir, "netstandard.dll")),
        };
        var compilation = CSharpCompilation.Create(
            "GenTest",
            trees,
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver.Create(new TypedIdGenerator());
        var result = driver.RunGenerators(compilation).GetRunResult();
        return result.Diagnostics;
    }
}
