using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ZeroAlloc.ValueObjects.Generator;

using ZeroAlloc.TestHelpers;

namespace ZeroAlloc.ValueObjects.Tests.GeneratorTests;

public class SinglePropertyStructTests
{
    [Fact]
    public void GeneratesClass_ForSingleStringProperty()
    {
        var source = """
            using ZeroAlloc.ValueObjects;

            [ValueObject]
            public partial class EmailAddress
            {
                public string Value { get; }
            }
            """;

        GeneratorSnapshot.Verify(RunGenerator(source));
    }

    [Fact]
    public void GeneratesReadonlyStruct_ForStructDeclaration()
    {
        var source = """
            using ZeroAlloc.ValueObjects;

            [ValueObject]
            public partial struct Amount
            {
                public decimal Value { get; }
            }
            """;

        GeneratorSnapshot.Verify(RunGenerator(source));
    }

    [Fact]
    public void GeneratesClass_WhenForceClassIsTrue_OnStruct()
    {
        var source = """
            using ZeroAlloc.ValueObjects;

            [ValueObject(ForceClass = true)]
            public partial struct OrderId
            {
                public int Value { get; }
            }
            """;

        GeneratorSnapshot.Verify(RunGenerator(source));
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
