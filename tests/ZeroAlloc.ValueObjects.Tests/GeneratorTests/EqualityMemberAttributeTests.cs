using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using VerifyXunit;
using ZeroAlloc.ValueObjects.Generator;

namespace ZeroAlloc.ValueObjects.Tests.GeneratorTests;

public class EqualityMemberAttributeTests
{
    [Fact]
    public Task GeneratesEquality_OnlyForMarkedMembers()
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

        return Verifier.Verify(RunGenerator(source));
    }

    [Fact]
    public Task GeneratesEquality_ExcludingIgnoredMembers()
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
