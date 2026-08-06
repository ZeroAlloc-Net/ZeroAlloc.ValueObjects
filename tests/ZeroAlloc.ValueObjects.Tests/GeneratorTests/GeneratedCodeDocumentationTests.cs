using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ZeroAlloc.ValueObjects.Generator;

namespace ZeroAlloc.ValueObjects.Tests.GeneratorTests;

/// <summary>
/// Guards issue #55: the &lt;auto-generated&gt; header suppresses analyzer diagnostics but not
/// compiler ones, so CS1591 fired on every generated public member for consumers who enable
/// GenerateDocumentationFile — a build error under TreatWarningsAsErrors.
/// </summary>
public sealed class GeneratedCodeDocumentationTests
{
    [Fact]
    public void ValueObject_GeneratedCode_ProducesNoCS1591()
    {
        var cs1591 = GetGeneratedDocumentationWarnings(
            new ValueObjectGenerator(),
            """
            using ZeroAlloc.ValueObjects;

            /// <summary>A documented consumer type.</summary>
            [ValueObject]
            public partial class Money
            {
                /// <summary>Amount.</summary>
                public decimal Amount { get; }
                /// <summary>Currency.</summary>
                public string Currency { get; } = "";
            }
            """);

        Assert.Empty(cs1591);
    }

    [Fact]
    public void TypedIdGuid_GeneratedCode_ProducesNoCS1591()
    {
        var cs1591 = GetGeneratedDocumentationWarnings(
            new TypedIdGenerator(),
            """
            using ZeroAlloc.ValueObjects;

            /// <summary>A documented consumer id.</summary>
            [TypedId(Strategy = IdStrategy.Ulid)]
            public readonly partial record struct DocumentId;
            """);

        Assert.Empty(cs1591);
    }

    [Fact]
    public void TypedIdInt64_GeneratedCode_ProducesNoCS1591()
    {
        var cs1591 = GetGeneratedDocumentationWarnings(
            new TypedIdGenerator(),
            """
            using ZeroAlloc.ValueObjects;

            /// <summary>A documented consumer id.</summary>
            [TypedId(Strategy = IdStrategy.Snowflake, Backing = BackingType.Int64)]
            public readonly partial record struct OrderId;
            """);

        Assert.Empty(cs1591);
    }

    /// <summary>
    /// Runs <paramref name="generator"/> over <paramref name="source"/> in a compilation that
    /// requests XML documentation, and returns any CS1591 reported against generator output.
    /// Diagnostics on the consumer's own source are excluded — only generated trees are asserted on.
    /// </summary>
    private static IReadOnlyList<Diagnostic> GetGeneratedDocumentationWarnings(
        IIncrementalGenerator generator,
        string source)
    {
        // DocumentationMode.Diagnose is what makes the compiler report CS1591; it is the
        // parse-options equivalent of <GenerateDocumentationFile>true</GenerateDocumentationFile>.
        var parseOptions = new CSharpParseOptions(documentationMode: DocumentationMode.Diagnose);
        var sourceTree = CSharpSyntaxTree.ParseText(source, parseOptions, path: "Source.cs");

        var runtimeDir = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();
        var refs = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(TypedIdAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
            MetadataReference.CreateFromFile(System.IO.Path.Combine(runtimeDir, "System.Runtime.dll")),
            MetadataReference.CreateFromFile(System.IO.Path.Combine(runtimeDir, "netstandard.dll")),
            MetadataReference.CreateFromFile(System.IO.Path.Combine(runtimeDir, "System.Text.Json.dll")),
            MetadataReference.CreateFromFile(System.IO.Path.Combine(runtimeDir, "System.Collections.dll")),
        };

        var compilation = CSharpCompilation.Create(
            "DocGenTest",
            new[] { sourceTree },
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // The driver needs the same parse options, otherwise generated trees are parsed with
        // DocumentationMode.Parse and CS1591 is never evaluated against them.
        CSharpGeneratorDriver
            .Create(new[] { generator.AsSourceGenerator() }, parseOptions: parseOptions)
            .RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);

        var diagnostics = outputCompilation.GetDiagnostics();
        var results = new List<Diagnostic>();
        foreach (var d in diagnostics)
        {
            if (!string.Equals(d.Id, "CS1591", StringComparison.Ordinal))
                continue;

            var tree = d.Location.SourceTree;
            if (tree is not null && tree != sourceTree)
                results.Add(d);
        }

        return results;
    }
}
