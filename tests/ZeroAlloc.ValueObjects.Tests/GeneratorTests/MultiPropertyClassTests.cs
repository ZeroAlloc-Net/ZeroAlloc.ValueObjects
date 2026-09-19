using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ZeroAlloc.ValueObjects.Generator;

using ZeroAlloc.TestHelpers;

namespace ZeroAlloc.ValueObjects.Tests.GeneratorTests;

public class MultiPropertyClassTests
{
    [Fact]
    public void GeneratesEquality_ForMultiPropertyClass()
    {
        var source = """
            using ZeroAlloc.ValueObjects;

            [ValueObject]
            public partial class Money
            {
                public decimal Amount { get; }
                public string Currency { get; }
            }
            """;

        var compilation = CreateCompilation(source);
        var generator = new ValueObjectGenerator();
        var driver = CSharpGeneratorDriver.Create(generator)
            .RunGenerators(compilation);

        GeneratorSnapshot.Verify(driver);
    }

    private static CSharpCompilation CreateCompilation(string source) =>
        CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
            references: [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(ValueObjectAttribute).Assembly.Location),
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
}
