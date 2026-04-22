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
        // Ulid → Guid backing, uses UlidCore for generation.
        Assert.Contains("public Guid Value", generated, StringComparison.Ordinal);
        Assert.Contains("UlidCore.NewGuid()", generated, StringComparison.Ordinal);
        Assert.Contains("UlidCore.ToBase32(Value)", generated, StringComparison.Ordinal);
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
        // Uuid7 → Guid backing, uses Uuid7Core for generation.
        Assert.Contains("public Guid Value", generated, StringComparison.Ordinal);
        Assert.Contains("Uuid7Core.NewGuid()", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void NoArgs_WithAssemblyDefaultSnowflake_ResolvesToSnowflakeInt64()
    {
        var source = """
            using ZeroAlloc.ValueObjects;
            [assembly: TypedIdDefault(Strategy = IdStrategy.Snowflake)]
            namespace MyApp;
            [TypedId]
            public readonly partial record struct Id;
            """;
        var generated = Generate(source);
        // Assembly default Snowflake → Int64 backing, uses SnowflakeCore.
        Assert.Contains("public long Value", generated, StringComparison.Ordinal);
        Assert.Contains("SnowflakeCore.Next", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void Generator_EmitsComparisonOperators()
    {
        var source = """
            using ZeroAlloc.ValueObjects;
            namespace MyApp;
            [TypedId]
            public readonly partial record struct OrderId;
            """;
        var generated = Generate(source);
        // MA0097 requires these on types implementing IComparable<T>.
        Assert.Contains("operator <(", generated, StringComparison.Ordinal);
        Assert.Contains("operator >(", generated, StringComparison.Ordinal);
        Assert.Contains("operator <=(", generated, StringComparison.Ordinal);
        Assert.Contains("operator >=(", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void Generator_EmitsComparisonOperators_ForInt64Strategy()
    {
        var source = """
            using ZeroAlloc.ValueObjects;
            namespace MyApp;
            [TypedId(Strategy = IdStrategy.Snowflake)]
            public readonly partial record struct MessageId;
            """;
        var generated = Generate(source);
        Assert.Contains("operator <(", generated, StringComparison.Ordinal);
        Assert.Contains("operator >(", generated, StringComparison.Ordinal);
        Assert.Contains("operator <=(", generated, StringComparison.Ordinal);
        Assert.Contains("operator >=(", generated, StringComparison.Ordinal);
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
        // Snowflake → Int64 backing, uses SnowflakeCore with worker id resolution.
        Assert.Contains("public long Value", generated, StringComparison.Ordinal);
        Assert.Contains("SnowflakeCore.Next(workerId)", generated, StringComparison.Ordinal);
        Assert.Contains("ResolveWorkerId", generated, StringComparison.Ordinal);
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
