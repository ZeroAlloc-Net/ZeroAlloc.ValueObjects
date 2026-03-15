using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using VerifyXunit;
using ZeroAlloc.ValueObjects.Generator;

namespace ZeroAlloc.ValueObjects.Tests.GeneratorTests;

public class SinglePropertyStructTests
{
    [Fact]
    public Task GeneratesClass_ForSingleStringProperty()
    {
        var source = """
            using ZeroAlloc.ValueObjects;

            [ValueObject]
            public partial class EmailAddress
            {
                public string Value { get; }
            }
            """;

        return Verifier.Verify(RunGenerator(source));
    }

    [Fact]
    public Task GeneratesReadonlyStruct_ForStructDeclaration()
    {
        var source = """
            using ZeroAlloc.ValueObjects;

            [ValueObject]
            public partial struct Amount
            {
                public decimal Value { get; }
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
