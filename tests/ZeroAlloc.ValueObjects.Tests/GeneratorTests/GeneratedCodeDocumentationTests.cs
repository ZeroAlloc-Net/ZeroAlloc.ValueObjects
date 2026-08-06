using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ZeroAlloc.ValueObjects.Generator;

namespace ZeroAlloc.ValueObjects.Tests.GeneratorTests;

/// <summary>
/// Guards issue #55 and the documentation emitted to close it.
/// <para>
/// Two properties are asserted. First, generated code raises no documentation diagnostic — the
/// &lt;auto-generated&gt; header suppresses analyzer diagnostics but not compiler ones, so CS1591
/// fired on every generated public member for consumers who enable GenerateDocumentationFile.
/// Second, the generated members actually appear in the consumer's XML documentation file, which
/// suppressing CS1591 alone would not achieve.
/// </para>
/// </summary>
public sealed class GeneratedCodeDocumentationTests
{
    // CS1591 missing comment, CS1570 malformed XML, CS1571 duplicate param, CS1572 unknown param,
    // CS1573 missing param, CS1574/CS1580/CS1581 unresolvable cref, CS1584 malformed cref.
    // A generator that emits a bad cref would break consumers exactly like the original bug did.
    private static readonly HashSet<string> s_docDiagnosticIds = new(StringComparer.Ordinal)
    {
        "CS1570", "CS1571", "CS1572", "CS1573", "CS1574", "CS1580", "CS1581", "CS1584", "CS1591",
    };

    private const string ValueObjectSource = """
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
        """;

    private const string TypedIdGuidSource = """
        using ZeroAlloc.ValueObjects;

        /// <summary>A documented consumer id.</summary>
        [TypedId(Strategy = IdStrategy.Ulid)]
        public readonly partial record struct DocumentId;
        """;

    private const string TypedIdInt64Source = """
        using ZeroAlloc.ValueObjects;

        /// <summary>A documented consumer id.</summary>
        [TypedId(Strategy = IdStrategy.Snowflake, Backing = BackingType.Int64)]
        public readonly partial record struct OrderId;
        """;

    [Fact]
    public void ValueObject_GeneratedCode_RaisesNoDocumentationDiagnostics()
        => Assert.Empty(Compile(new ValueObjectGenerator(), ValueObjectSource).DocDiagnostics);

    [Fact]
    public void TypedIdGuid_GeneratedCode_RaisesNoDocumentationDiagnostics()
        => Assert.Empty(Compile(new TypedIdGenerator(), TypedIdGuidSource).DocDiagnostics);

    [Fact]
    public void TypedIdInt64_GeneratedCode_RaisesNoDocumentationDiagnostics()
        => Assert.Empty(Compile(new TypedIdGenerator(), TypedIdInt64Source).DocDiagnostics);

    [Theory]
    // Operators inherit no documentation, so they are the members most at risk of being missed.
    [InlineData("M:Money.op_Equality(Money,Money)")]
    [InlineData("M:Money.op_Inequality(Money,Money)")]
    [InlineData("M:Money.Equals(System.Object)")]
    [InlineData("M:Money.GetHashCode")]
    [InlineData("M:Money.ToString")]
    public void ValueObject_GeneratedMembers_AppearInXmlDocumentation(string memberId)
        => Assert.Contains(memberId, Compile(new ValueObjectGenerator(), ValueObjectSource).Xml, StringComparison.Ordinal);

    [Theory]
    [InlineData("P:DocumentId.Value")]
    [InlineData("M:DocumentId.#ctor(System.Guid)")]
    [InlineData("M:DocumentId.New")]
    [InlineData("M:DocumentId.ToString")]
    [InlineData("M:DocumentId.op_LessThan(DocumentId,DocumentId)")]
    [InlineData("T:DocumentId.TypedIdJsonConverter")]
    public void TypedIdGuid_GeneratedMembers_AppearInXmlDocumentation(string memberId)
        => Assert.Contains(memberId, Compile(new TypedIdGenerator(), TypedIdGuidSource).Xml, StringComparison.Ordinal);

    [Theory]
    [InlineData("P:OrderId.Value")]
    [InlineData("M:OrderId.#ctor(System.Int64)")]
    [InlineData("M:OrderId.New")]
    [InlineData("M:OrderId.ToString")]
    [InlineData("T:OrderId.TypedIdJsonConverter")]
    public void TypedIdInt64_GeneratedMembers_AppearInXmlDocumentation(string memberId)
        => Assert.Contains(memberId, Compile(new TypedIdGenerator(), TypedIdInt64Source).Xml, StringComparison.Ordinal);

    private sealed record CompileResult(IReadOnlyList<Diagnostic> DocDiagnostics, string Xml);

    /// <summary>
    /// Runs <paramref name="generator"/> over <paramref name="source"/> in a compilation that
    /// requests XML documentation, then emits it to capture the resulting documentation file.
    /// </summary>
    private static CompileResult Compile(IIncrementalGenerator generator, string source)
    {
        // DocumentationMode.Diagnose is the parse-options equivalent of
        // <GenerateDocumentationFile>true</GenerateDocumentationFile>.
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
        // DocumentationMode.Parse and documentation diagnostics are never evaluated against them.
        CSharpGeneratorDriver
            .Create(new[] { generator.AsSourceGenerator() }, parseOptions: parseOptions)
            .RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);

        using var peStream = new System.IO.MemoryStream();
        using var xmlStream = new System.IO.MemoryStream();
        var emitResult = outputCompilation.Emit(peStream, xmlDocumentationStream: xmlStream);

        var docDiagnostics = new List<Diagnostic>();
        foreach (var d in emitResult.Diagnostics)
        {
            if (!s_docDiagnosticIds.Contains(d.Id))
                continue;

            // Only assert on generator output; the consumer's own source is not our concern.
            var tree = d.Location.SourceTree;
            if (tree is not null && tree != sourceTree)
                docDiagnostics.Add(d);
        }

        // A failed emit would yield a misleading empty documentation file, so surface it.
        Assert.True(
            emitResult.Success,
            "Generated code failed to compile: " + string.Join("; ", DescribeErrors(emitResult.Diagnostics)));

        return new CompileResult(docDiagnostics, System.Text.Encoding.UTF8.GetString(xmlStream.ToArray()));
    }

    private static List<string> DescribeErrors(IEnumerable<Diagnostic> diagnostics)
    {
        var errors = new List<string>();
        foreach (var d in diagnostics)
        {
            if (d.Severity == DiagnosticSeverity.Error)
                errors.Add(d.ToString());
        }
        return errors;
    }
}
