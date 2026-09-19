using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ZeroAlloc.ValueObjects.Generator;

using ZeroAlloc.TestHelpers;

namespace ZeroAlloc.ValueObjects.Tests.GeneratorTests;

public class NullablePropertyTests
{
    [Fact]
    public void GeneratesNullSafeComparison_ForNullableProperties()
    {
        var source = """
            #nullable enable
            using ZeroAlloc.ValueObjects;

            [ValueObject]
            public partial class Contact
            {
                public string Name { get; }
                public string? Email { get; }
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
