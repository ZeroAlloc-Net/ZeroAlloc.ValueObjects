using System;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ZeroAlloc.TestHelpers;
using ZeroAlloc.ValueObjects.Generator;

namespace ZeroAlloc.ValueObjects.Tests.GeneratorTests;

/// <summary>
/// A nested or generic value object is generated into the value object itself, inside partial
/// declarations of its containing types, and not into a new top-level, non-generic type with its
/// simple name (#196). A value object that cannot be reopened is reported and not generated.
/// </summary>
public class NestedValueObjectTests
{
    [Fact]
    public void NestedValueObjects_WithTheSameName_AreGeneratedIntoTheirOwnTypes()
    {
        var run = GeneratorRun.Of(new ValueObjectGenerator(), """
            using ZeroAlloc.ValueObjects;
            namespace App
            {
                public partial class Orders
                {
                    [ValueObject] public partial class Money { public Money(int v) => Value = v; public int Value { get; } }
                }
                public partial class Invoices
                {
                    public partial record Inner
                    {
                        [ValueObject] public partial struct Money { public Money(int v) => Value = v; public int Value { get; } }
                    }
                }
                public static class Calls
                {
                    public static bool Run() =>
                        new Orders.Money(1) == new Orders.Money(1)
                        && new Invoices.Inner.Money(2).Equals(new Invoices.Inner.Money(2));
                }
            }
            """);

        Assert.Empty(run.GeneratorDiagnostics);
        Assert.Empty(run.Errors);
        Assert.Empty(run.Warnings);
        Assert.Null(run.Output.GetTypeByMetadataName("App.Money"));
        AssertEquatable(run, "App.Orders+Money");
        AssertEquatable(run, "App.Invoices+Inner+Money");
    }

    [Fact]
    public void GenericValueObjects_AreGeneratedWithTheirTypeParameters()
    {
        var run = GeneratorRun.Of(new ValueObjectGenerator(), """
            using ZeroAlloc.ValueObjects;
            namespace App
            {
                [ValueObject] public partial class Range<T> { public Range(int v) => Value = v; public int Value { get; } }
                [ValueObject] public partial class Pair<TKey, TValue> where TKey : struct where TValue : class
                {
                    public Pair(TKey k, TValue v) { Key = k; Val = v; }
                    public TKey Key { get; }
                    public TValue Val { get; }
                }
                [ValueObject] public partial struct Box<T>
                {
                    public Box(T v) => Item = v;
                    public T Item { get; }
                }
                [ValueObject] public partial class Maybe<T>
                {
                    public Maybe(T? v) => Item = v;
                    public T? Item { get; }
                }
                [ValueObject] public partial class Many<T>
                {
                    public Many(T a, T b, T? c) { A = a; B = b; C = c; }
                    public T A { get; }
                    public T B { get; }
                    public T? C { get; }
                }
                public partial class Outer<T>
                {
                    [ValueObject] public partial class Money { public Money(T v) => Item = v; public T Item { get; } }
                }
                public static class Calls
                {
                    public static bool Run() =>
                        new Range<string>(1) == new Range<string>(1)
                        && new Pair<int, string>(1, "a") == new Pair<int, string>(1, "a")
                        && new Box<string?>(null) == new Box<string?>(null)
                        && new Maybe<int>(1) == new Maybe<int>(1)
                        && new Many<string>("a", "b", null) == new Many<string>("a", "b", null)
                        && new Outer<long>.Money(3) == new Outer<long>.Money(3)
                        && new Box<int>(1).GetHashCode() == new Box<int>(1).GetHashCode()
                        && new Box<string>("x").ToString() == "x";
                }
            }
            """);

        Assert.Empty(run.GeneratorDiagnostics);
        Assert.Empty(run.Errors);
        Assert.Empty(run.Warnings);
        Assert.Null(run.Output.GetTypeByMetadataName("App.Range"));
        AssertEquatable(run, "App.Range`1");
        AssertEquatable(run, "App.Pair`2");
        AssertEquatable(run, "App.Box`1");
        AssertEquatable(run, "App.Outer`1+Money");
    }

    [Fact]
    public void ValueObjectsInEveryKindOfContainingType_AreGeneratedIntoTheirOwnTypes()
    {
        var run = GeneratorRun.Of(new ValueObjectGenerator(), """
            using ZeroAlloc.ValueObjects;
            namespace App
            {
                public partial struct Holder<T> where T : class, new()
                {
                    [ValueObject] public partial class M<U> where U : struct { public int Value { get; } }
                }
                public partial interface IHolder<T>
                {
                    [ValueObject] public partial class M { public int Value { get; } }
                }
                public partial record struct Pair
                {
                    [ValueObject] public partial class M { public int Value { get; } }
                }
                public readonly ref partial struct Span
                {
                    [ValueObject] public partial class M { public int Value { get; } }
                }
                public sealed partial record Rec(int Z)
                {
                    [ValueObject] public partial class M { public int Value { get; } }
                }
                public static partial class Static
                {
                    [ValueObject] internal partial struct M { public int Value { get; } }
                }
            }
            """);

        Assert.Empty(run.GeneratorDiagnostics);
        Assert.Equal(6, run.HintNames.Count);
        Assert.Empty(run.Errors);
        AssertEquatable(run, "App.Holder`1+M`1");
        AssertEquatable(run, "App.IHolder`1+M");
        AssertEquatable(run, "App.Pair+M");
        AssertEquatable(run, "App.Span+M");
        AssertEquatable(run, "App.Rec+M");
        AssertEquatable(run, "App.Static+M");
    }

    [Fact]
    public void VerbatimNamedValueObjectAndContainers_AreGeneratedIntoTheirOwnTypes()
    {
        var run = GeneratorRun.Of(new ValueObjectGenerator(), """
            using ZeroAlloc.ValueObjects;
            namespace App
            {
                public partial class @class<@int>
                {
                    [ValueObject] public partial class @static { public int Value { get; } }
                }
                [ValueObject] public partial class @event<@void> { public int Value { get; } }
            }
            """);

        Assert.Empty(run.GeneratorDiagnostics);
        Assert.Empty(run.Errors);
        AssertEquatable(run, "App.class`1+static");
        AssertEquatable(run, "App.event`1");
    }

    [Fact]
    public void ContainingTypeDeclaredInSeveralParts_IsPartial()
    {
        var run = GeneratorRun.Of(new ValueObjectGenerator(), """
            using ZeroAlloc.ValueObjects;
            namespace App
            {
                public partial class Outer { }
            }
            """, """
            using ZeroAlloc.ValueObjects;
            namespace App
            {
                public partial class Outer
                {
                    [ValueObject] public partial class Money { public int Value { get; } }
                }
            }
            """);

        Assert.Empty(run.GeneratorDiagnostics);
        Assert.Empty(run.Errors);
        AssertEquatable(run, "App.Outer+Money");
    }

    [Fact]
    public void NestedGenericValueObject_EmitsThePartialDeclarationsOfItsContainingTypes()
    {
        var source = """
            using ZeroAlloc.ValueObjects;
            namespace App;
            public partial struct Holder<T>
            {
                public static partial class Values<U>
                {
                    [ValueObject] public partial class Money<V> { public V Amount { get; } public string Currency { get; } }
                }
            }
            """;

        GeneratorSnapshot.Verify(RunDriver(source));
    }

    [Fact]
    public void ValueObjectInANonPartialContainingType_ReportsZavo001_AndGeneratesNothing()
    {
        var source = """
            using ZeroAlloc.ValueObjects;
            namespace App
            {
                public class Outer
                {
                    [ValueObject] public partial class Money { public int Value { get; } }
                }
            }
            """;

        var run = GeneratorRun.Of(new ValueObjectGenerator(), source);

        var zavo001 = AssertEx.One(run.GeneratorDiagnostics);
        Assert.Equal("ZAVO001", zavo001.Id);
        Assert.Equal(DiagnosticSeverity.Warning, zavo001.Severity);
        Assert.Equal(
            "Value object 'App.Outer.Money' is not generated because its containing type 'App.Outer' is not partial",
            zavo001.GetMessage(CultureInfo.InvariantCulture));
        var span = zavo001.Location.SourceSpan;
        Assert.Equal("Money", source.Substring(span.Start, span.Length));
        Assert.Empty(run.HintNames);
        Assert.Empty(run.Errors);
    }

    [Fact]
    public void ValueObjectWhoseOuterContainingTypeIsNotPartial_ReportsZavo001_NamingThatType()
    {
        var run = GeneratorRun.Of(new ValueObjectGenerator(), """
            using ZeroAlloc.ValueObjects;
            namespace App
            {
                public static class Outer
                {
                    public partial class Middle
                    {
                        [ValueObject] public partial class Money { public int Value { get; } }
                    }
                }
            }
            """);

        var zavo001 = AssertEx.One(run.GeneratorDiagnostics);
        Assert.Equal("ZAVO001", zavo001.Id);
        Assert.Contains(
            "containing type 'App.Outer' is not partial",
            zavo001.GetMessage(CultureInfo.InvariantCulture),
            StringComparison.Ordinal);
        Assert.Empty(run.HintNames);
        Assert.Empty(run.Errors);
    }

    [Fact]
    public void FileLocalValueObject_ReportsZavo002_AndGeneratesNothing()
    {
        var source = """
            using ZeroAlloc.ValueObjects;
            namespace App;
            [ValueObject] file partial class Money { public int Value { get; } }
            file partial class Outer
            {
                [ValueObject] public partial class Amount { public int Value { get; } }
            }
            [ValueObject] public partial class Kept { public int Value { get; } }
            """;

        var run = GeneratorRun.Of(new ValueObjectGenerator(), source);

        Assert.Equal(2, run.GeneratorDiagnostics.Length);
        Assert.All(run.GeneratorDiagnostics, d =>
        {
            Assert.Equal("ZAVO002", d.Id);
            Assert.Equal(DiagnosticSeverity.Error, d.Severity);
        });
        var money = AssertEx.One(run.GeneratorDiagnostics, d => string.Equals(source.Substring(d.Location.SourceSpan.Start, d.Location.SourceSpan.Length), "Money", StringComparison.Ordinal));
        Assert.Equal(
            "Value object 'App.Money' is not generated because 'App.Money' is file-local",
            money.GetMessage(CultureInfo.InvariantCulture));
        var amount = AssertEx.One(run.GeneratorDiagnostics, d => !d.Equals(money));
        Assert.Equal(
            "Value object 'App.Outer.Amount' is not generated because 'App.Outer' is file-local",
            amount.GetMessage(CultureInfo.InvariantCulture));
        Assert.Equal(["App.Kept.g.cs"], run.HintNames);
    }

    [Fact]
    public void ValueObjectsDifferingOnlyInCase_ReportZavo003_OnTheLaterOne_AndGenerateTheRest()
    {
        var source = """
            using ZeroAlloc.ValueObjects;
            namespace App
            {
                [ValueObject] public partial class Money { public int Value { get; } }
                [ValueObject] public partial class money { public int Value { get; } }
                [ValueObject] public partial class Other { public int Value { get; } }
            }
            """;

        var run = GeneratorRun.Of(new ValueObjectGenerator(), source);

        var zavo003 = AssertEx.One(run.GeneratorDiagnostics);
        Assert.Equal("ZAVO003", zavo003.Id);
        Assert.Equal(DiagnosticSeverity.Error, zavo003.Severity);
        Assert.Equal(
            "Value object 'App.money' is not generated because its file name 'App.money.g.cs' differs only in case from that of 'App.Money'",
            zavo003.GetMessage(CultureInfo.InvariantCulture));
        var span = zavo003.Location.SourceSpan;
        Assert.Equal("money", source.Substring(span.Start, span.Length));
        Assert.Equal(["App.Money.g.cs", "App.Other.g.cs"], run.HintNames);
        Assert.DoesNotContain(run.GeneratorDiagnostics, d => string.Equals(d.Id, "CS8785", StringComparison.Ordinal));
    }

    /// <summary>
    /// The earlier type is the one declared first, by file path and then position, so which one
    /// is generated does not depend on the order of the syntax trees in the compilation.
    /// </summary>
    [Fact]
    public void EarlierValueObject_IsTheOneDeclaredFirst_AcrossFiles()
    {
        var first = CSharpSyntaxTree.ParseText(
            "using ZeroAlloc.ValueObjects; namespace App; [ValueObject] public partial class money { public int Value { get; } }",
            path: "a.cs");
        var second = CSharpSyntaxTree.ParseText(
            "using ZeroAlloc.ValueObjects; namespace App; [ValueObject] public partial class Money { public int Value { get; } }",
            path: "b.cs");

        var result = CSharpGeneratorDriver.Create(new ValueObjectGenerator())
            .RunGenerators(TypedIdDiagnosticTests.CreateCompilation(second, first))
            .GetRunResult()
            .Results[0];

        Assert.Equal("App.money.g.cs", AssertEx.One(result.GeneratedSources).HintName);
        var zavo003 = AssertEx.One(result.Diagnostics, d => string.Equals(d.Id, "ZAVO003", StringComparison.Ordinal));
        Assert.Equal("b.cs", zavo003.Location.SourceTree!.FilePath);
    }

    private static void AssertEquatable(GeneratorRun run, string metadataName)
    {
        var type = run.Output.GetTypeByMetadataName(metadataName);
        Assert.NotNull(type);
        Assert.Contains(type!.AllInterfaces, i =>
            string.Equals(i.OriginalDefinition.ToDisplayString(), "System.IEquatable<T>", StringComparison.Ordinal)
            && SymbolEqualityComparer.Default.Equals(i.TypeArguments[0], type));
    }

    private static GeneratorDriver RunDriver(string source)
    {
        var compilation = CSharpCompilation.Create("TestAssembly",
            [CSharpSyntaxTree.ParseText(source)],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
             MetadataReference.CreateFromFile(typeof(ValueObjectAttribute).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return CSharpGeneratorDriver.Create(new ValueObjectGenerator()).RunGenerators(compilation);
    }
}
