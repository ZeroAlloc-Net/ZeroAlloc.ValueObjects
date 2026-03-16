using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using VerifyXunit;
using ZeroAlloc.ValueObjects.Generator;

namespace ZeroAlloc.ValueObjects.Tests.GeneratorTests;

public class EdgeCaseTests
{
    [Fact]
    public Task GeneratesEquality_ForZeroProperties()
    {
        var source = """
            using ZeroAlloc.ValueObjects;

            [ValueObject]
            public partial class Empty
            {
            }
            """;

        return Verifier.Verify(RunGenerator(source));
    }

    [Fact]
    public Task GeneratesHashCodeAdd_ForNineOrMoreProperties()
    {
        var source = """
            using ZeroAlloc.ValueObjects;

            [ValueObject]
            public partial class Wide
            {
                public int A { get; }
                public int B { get; }
                public int C { get; }
                public int D { get; }
                public int E { get; }
                public int F { get; }
                public int G { get; }
                public int H { get; }
                public int I { get; }
            }
            """;

        return Verifier.Verify(RunGenerator(source));
    }

    [Fact]
    public Task GeneratesEquality_ForTypeInGlobalNamespace()
    {
        var source = """
            using ZeroAlloc.ValueObjects;

            [ValueObject]
            public partial class GlobalType
            {
                public string Value { get; }
            }
            """;

        return Verifier.Verify(RunGenerator(source));
    }

    private static GeneratorDriver RunGenerator(string source)
    {
        var compilation = CSharpCompilation.Create("TestAssembly",
            [CSharpSyntaxTree.ParseText(source)],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
             MetadataReference.CreateFromFile(typeof(ValueObjectAttribute).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return CSharpGeneratorDriver.Create(new ValueObjectGenerator()).RunGenerators(compilation);
    }
}
