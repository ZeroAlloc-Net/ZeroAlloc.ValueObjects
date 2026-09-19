using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ZeroAlloc.ValueObjects.Generator;

using ZeroAlloc.TestHelpers;

namespace ZeroAlloc.ValueObjects.Tests.GeneratorTests;

public class EqualityMemberAttributeTests
{
    [Fact]
    public void GeneratesEquality_OnlyForMarkedMembers()
    {
        var source = """
            using ZeroAlloc.ValueObjects;

            [ValueObject]
            public partial class Address
            {
                [EqualityMember] public string Street { get; }
                [EqualityMember] public string City { get; }
                public string Notes { get; }
            }
            """;

        GeneratorSnapshot.Verify(RunGenerator(source));
    }

    [Fact]
    public void GeneratesEquality_ExcludingIgnoredMembers()
    {
        var source = """
            using ZeroAlloc.ValueObjects;

            [ValueObject]
            public partial class Product
            {
                public string Name { get; }
                [IgnoreEqualityMember] public string InternalCode { get; }
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
