using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ZeroAlloc.ValueObjects.Generator;

namespace ZeroAlloc.ValueObjects.Tests.GeneratorTests;

/// <summary>
/// Every generated file needs a hint name that is unique in the compilation. A duplicate makes
/// <c>AddSource</c> throw, and the generator then fails with CS8785 and emits nothing at all.
/// </summary>
public class HintNameTests
{
    [Fact]
    public void ValueObject_NamespaceAndTypeNamesThatJoinWithUnderscoreAlike_GetDistinctFiles()
    {
        var source = """
            using ZeroAlloc.ValueObjects;

            namespace A_B
            {
                [ValueObject]
                public partial class C { public int Value { get; } }
            }

            namespace A
            {
                [ValueObject]
                public partial class B_C { public int Value { get; } }
            }
            """;

        var result = RunValueObject(source);

        AssertNoGeneratorFailure(result);
        Assert.Equal(["A.B_C.g.cs", "A_B.C.g.cs"], HintNames(result));
    }

    [Fact]
    public void ValueObject_SameNameInDifferentContainingTypes_GetsDistinctFiles()
    {
        var source = """
            using ZeroAlloc.ValueObjects;

            namespace App;

            public partial class Orders
            {
                [ValueObject]
                public partial class Money { public int Value { get; } }
            }

            public partial class Invoices
            {
                [ValueObject]
                public partial class Money { public int Value { get; } }
            }
            """;

        var result = RunValueObject(source);

        AssertNoGeneratorFailure(result);
        Assert.Equal(["App.Invoices+Money.g.cs", "App.Orders+Money.g.cs"], HintNames(result));
    }

    [Fact]
    public void ValueObject_GenericAndNonGenericWithTheSameName_GetDistinctFiles()
    {
        var source = """
            using ZeroAlloc.ValueObjects;

            namespace App;

            [ValueObject]
            public partial class Range { public int Value { get; } }

            [ValueObject]
            public partial class Range<T> { public int Value { get; } }
            """;

        var result = RunValueObject(source);

        AssertNoGeneratorFailure(result);
        Assert.Equal(["App.Range.g.cs", "App.Range`1.g.cs"], HintNames(result));
    }

    [Fact]
    public void ValueObject_NestedTypeAndTypeInNamespaceOfTheSameName_GetDistinctFiles()
    {
        var source = """
            using ZeroAlloc.ValueObjects;

            namespace App
            {
                public partial class Outer
                {
                    [ValueObject]
                    public partial class Money { public int Value { get; } }
                }
            }

            namespace App.Outer
            {
                [ValueObject]
                public partial class Money { public int Value { get; } }
            }
            """;

        // C# rejects a type and a namespace that share a name, but the generator still runs
        // first, and a dot for nesting would give both files the name App.Outer.Money.g.cs.
        var result = RunValueObject(source);

        AssertNoGeneratorFailure(result);
        Assert.Equal(["App.Outer+Money.g.cs", "App.Outer.Money.g.cs"], HintNames(result));
    }

    [Fact]
    public void ValueObject_InGenericContainingType_CarriesTheContainersArity()
    {
        var source = """
            using ZeroAlloc.ValueObjects;

            namespace App;

            public partial class Outer<TKey, TValue>
            {
                [ValueObject]
                public partial class Money { public int Value { get; } }
            }
            """;

        Assert.Equal(["App.Outer`2+Money.g.cs"], HintNames(RunValueObject(source)));
    }

    [Fact]
    public void ValueObject_VerbatimAndNonAsciiNames_AreKeptWithoutTheAtSign()
    {
        var source = """
            using ZeroAlloc.ValueObjects;

            namespace @event.Überweisung;

            [ValueObject]
            public partial class @class { public int Value { get; } }
            """;

        Assert.Equal(["event.Überweisung.class.g.cs"], HintNames(RunValueObject(source)));
    }

    [Fact]
    public void ValueObject_InGlobalNamespace_HasNoNamespacePart()
    {
        var source = """
            using ZeroAlloc.ValueObjects;

            [ValueObject]
            public partial class Money { public int Value { get; } }
            """;

        Assert.Equal(["Money.g.cs"], HintNames(RunValueObject(source)));
    }

    [Fact]
    public void TypedId_NamespaceAndTypeNamesThatJoinWithUnderscoreAlike_GetDistinctFiles()
    {
        var source = """
            using ZeroAlloc.ValueObjects;

            namespace A_B
            {
                [TypedId] public readonly partial record struct C;
            }

            namespace A
            {
                [TypedId] public readonly partial record struct B_C;
            }
            """;

        var result = RunTypedId(source);

        AssertNoGeneratorFailure(result);
        Assert.Equal(["A.B_C.TypedId.g.cs", "A_B.C.TypedId.g.cs"], HintNames(result));
    }

    [Fact]
    public void TypedId_SameNameInDifferentContainingTypes_GetsDistinctFiles()
    {
        var source = """
            using ZeroAlloc.ValueObjects;

            namespace App;

            public partial class Orders
            {
                [TypedId] public readonly partial record struct Id;
            }

            public partial class Invoices
            {
                [TypedId] public readonly partial record struct Id;
            }
            """;

        var result = RunTypedId(source);

        AssertNoGeneratorFailure(result);
        Assert.Equal(["App.Invoices+Id.TypedId.g.cs", "App.Orders+Id.TypedId.g.cs"], HintNames(result));
    }

    [Fact]
    public void TypedId_GenericAndNonGenericWithTheSameName_GetDistinctFiles()
    {
        var source = """
            using ZeroAlloc.ValueObjects;

            namespace App;

            [TypedId] public readonly partial record struct Key;

            [TypedId] public readonly partial record struct Key<T>;
            """;

        var result = RunTypedId(source);

        AssertNoGeneratorFailure(result);
        Assert.Equal(["App.Key.TypedId.g.cs", "App.Key`1.TypedId.g.cs"], HintNames(result));
    }

    [Fact]
    public void TypedId_InGlobalNamespace_HasNoNamespacePart()
    {
        var source = """
            using ZeroAlloc.ValueObjects;

            [TypedId] public readonly partial record struct OrderId;
            """;

        Assert.Equal(["OrderId.TypedId.g.cs"], HintNames(RunTypedId(source)));
    }

    private static GeneratorDriverRunResult RunValueObject(string source) =>
        CSharpGeneratorDriver.Create(new ValueObjectGenerator())
            .RunGenerators(TypedIdDiagnosticTests.CreateCompilation(CSharpSyntaxTree.ParseText(source)))
            .GetRunResult();

    private static GeneratorDriverRunResult RunTypedId(string source) =>
        CSharpGeneratorDriver.Create(new TypedIdGenerator())
            .RunGenerators(TypedIdDiagnosticTests.CreateCompilation(CSharpSyntaxTree.ParseText(source)))
            .GetRunResult();

    private static void AssertNoGeneratorFailure(GeneratorDriverRunResult result)
    {
        Assert.Null(result.Results[0].Exception);
        Assert.DoesNotContain(result.Diagnostics, d => string.Equals(d.Id, "CS8785", System.StringComparison.Ordinal));
    }

    private static string[] HintNames(GeneratorDriverRunResult result) =>
        result.Results[0].GeneratedSources
            .Select(s => s.HintName)
            .OrderBy(n => n, System.StringComparer.Ordinal)
            .ToArray();
}
