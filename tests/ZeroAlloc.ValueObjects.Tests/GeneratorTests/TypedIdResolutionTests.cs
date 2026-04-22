using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ZeroAlloc.ValueObjects.Generator;

namespace ZeroAlloc.ValueObjects.Tests.GeneratorTests;

public sealed class TypedIdResolutionTests
{
    [Fact]
    public void EmptyTypedId_NoAssemblyDefault_ResolvesToUlidGuid()
    {
        var source = """
            using ZeroAlloc.ValueObjects;
            namespace MyApp;
            [TypedId]
            public readonly partial record struct OrderId;
            """;
        var generated = Generate(source);
        Assert.Contains("Strategy: Ulid, Backing: Guid", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyTypedId_WithAssemblyDefault_UsesAssemblyDefault()
    {
        var source = """
            using ZeroAlloc.ValueObjects;
            [assembly: TypedIdDefault(Strategy = IdStrategy.Uuid7)]
            namespace MyApp;
            [TypedId]
            public readonly partial record struct MessageId;
            """;
        var generated = Generate(source);
        Assert.Contains("Strategy: Uuid7", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplicitStrategy_OverridesAssemblyDefault()
    {
        var source = """
            using ZeroAlloc.ValueObjects;
            [assembly: TypedIdDefault(Strategy = IdStrategy.Uuid7)]
            namespace MyApp;
            [TypedId(Strategy = IdStrategy.Snowflake)]
            public readonly partial record struct MessageId;
            """;
        var generated = Generate(source);
        Assert.Contains("Strategy: Snowflake, Backing: Int64", generated, StringComparison.Ordinal);
    }

    private static string Generate(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
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
            new[] { syntaxTree },
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver.Create(new TypedIdGenerator());
        var result = driver.RunGenerators(compilation).GetRunResult();
        var generatedSources = result.Results[0].GeneratedSources;
        return generatedSources.Length == 0 ? "" : generatedSources[0].SourceText.ToString();
    }
}
