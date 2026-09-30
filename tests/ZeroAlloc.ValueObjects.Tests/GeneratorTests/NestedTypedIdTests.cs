using System;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ZeroAlloc.ValueObjects.Generator;

namespace ZeroAlloc.ValueObjects.Tests.GeneratorTests;

/// <summary>
/// A nested typed ID is generated into the typed ID itself, inside partial declarations of its
/// containing types, and not into a new top-level type with its simple name (#196). A typed ID
/// that cannot be generated is reported and nothing is generated for it.
/// </summary>
public class NestedTypedIdTests
{
    [Fact]
    public void NestedTypedIds_WithTheSameName_AreGeneratedIntoTheirOwnTypes()
    {
        var run = GeneratorRun.Of(new TypedIdGenerator(), """
            using ZeroAlloc.ValueObjects;
            namespace App
            {
                public partial class Orders
                {
                    [TypedId] public readonly partial record struct Id;
                }
                public partial class Invoices
                {
                    public partial record Inner
                    {
                        [TypedId(Strategy = IdStrategy.Sequential)] public readonly partial record struct Id;
                    }
                }
                public static class Calls
                {
                    public static bool Run() =>
                        Orders.Id.Parse(Orders.Id.New().ToString()) != default
                        && Invoices.Inner.Id.New() > default(Invoices.Inner.Id)
                        && System.Text.Json.JsonSerializer.Serialize(Orders.Id.New()).Length > 0;
                }
            }
            """);

        Assert.Empty(run.GeneratorDiagnostics);
        Assert.Empty(run.Errors);
        Assert.Empty(run.Warnings);
        Assert.Null(run.Output.GetTypeByMetadataName("App.Id"));
        AssertGenerated(run, "App.Orders+Id");
        AssertGenerated(run, "App.Invoices+Inner+Id");
    }

    [Fact]
    public void TypedIdsInEveryKindOfContainingType_AndOfEveryAccessibility_AreGenerated()
    {
        var run = GeneratorRun.Of(new TypedIdGenerator(), """
            using ZeroAlloc.ValueObjects;
            namespace App
            {
                public partial struct Holder
                {
                    [TypedId] internal readonly partial record struct Id;
                }
                public partial interface IHolder
                {
                    [TypedId] public readonly partial record struct Id;
                }
                public partial record struct Pair
                {
                    [TypedId] private readonly partial record struct Id;
                }
                public readonly ref partial struct Span
                {
                    [TypedId] public readonly partial record struct Id;
                }
                public partial class Base
                {
                    [TypedId] protected internal readonly partial record struct Id;
                    [TypedId] private protected readonly partial record struct Other;
                    [TypedId] protected readonly partial record struct Third;
                }
                public partial class @class
                {
                    [TypedId] public readonly partial record struct @event;
                }
                [TypedId] internal readonly partial record struct TopLevelInternal;
            }
            """);

        Assert.Empty(run.GeneratorDiagnostics);
        Assert.Equal(9, run.HintNames.Count);
        Assert.Empty(run.Errors);
        AssertGenerated(run, "App.Holder+Id");
        AssertGenerated(run, "App.IHolder+Id");
        AssertGenerated(run, "App.Pair+Id");
        AssertGenerated(run, "App.Span+Id");
        AssertGenerated(run, "App.Base+Third");
        AssertGenerated(run, "App.class+event");
        AssertGenerated(run, "App.TopLevelInternal");
    }

    [Fact]
    public void NestedTypedId_EmitsThePartialDeclarationsOfItsContainingTypes()
    {
        var source = """
            using ZeroAlloc.ValueObjects;
            namespace App;
            public partial record struct Orders
            {
                internal static partial class Keys
                {
                    [TypedId(Strategy = IdStrategy.Sequential)] private readonly partial record struct Id;
                }
            }
            """;

        var driver = CSharpGeneratorDriver.Create(new TypedIdGenerator())
            .RunGenerators(TypedIdDiagnosticTests.CreateCompilation(CSharpSyntaxTree.ParseText(source)));
        ZeroAlloc.TestHelpers.GeneratorSnapshot.Verify(driver);
    }

    [Fact]
    public void GenericTypedId_ReportsZati007_AndGeneratesNothing()
    {
        var source = """
            using ZeroAlloc.ValueObjects;
            namespace App
            {
                [TypedId] public readonly partial record struct Key<T>;
                public partial class Outer<T>
                {
                    [TypedId] public readonly partial record struct Id;
                }
                [TypedId] public readonly partial record struct Kept;
            }
            """;

        var run = GeneratorRun.Of(new TypedIdGenerator(), source);

        Assert.Equal(2, run.GeneratorDiagnostics.Length);
        Assert.All(run.GeneratorDiagnostics, d =>
        {
            Assert.Equal("ZATI007", d.Id);
            Assert.Equal(DiagnosticSeverity.Error, d.Severity);
        });
        var key = AssertEx.One(run.GeneratorDiagnostics, d => string.Equals(Text(source, d), "Key", StringComparison.Ordinal));
        Assert.Equal(
            "[TypedId] struct 'App.Key<T>' is not generated because 'App.Key<T>' is generic; a JsonConverterAttribute cannot name a converter for an open generic type",
            key.GetMessage(CultureInfo.InvariantCulture));
        var id = AssertEx.One(run.GeneratorDiagnostics, d => string.Equals(Text(source, d), "Id", StringComparison.Ordinal));
        Assert.Equal(
            "[TypedId] struct 'App.Outer<T>.Id' is not generated because 'App.Outer<T>' is generic; a JsonConverterAttribute cannot name a converter for an open generic type",
            id.GetMessage(CultureInfo.InvariantCulture));
        Assert.Equal(["App.Kept.TypedId.g.cs"], run.HintNames);
    }

    [Fact]
    public void TypedIdInANonPartialContainingType_ReportsZati006_AndGeneratesNothing()
    {
        var source = """
            using ZeroAlloc.ValueObjects;
            namespace App
            {
                public static class Outer
                {
                    public partial class Middle
                    {
                        [TypedId] public readonly partial record struct Id;
                    }
                }
            }
            """;

        var run = GeneratorRun.Of(new TypedIdGenerator(), source);

        var zati006 = AssertEx.One(run.GeneratorDiagnostics);
        Assert.Equal("ZATI006", zati006.Id);
        Assert.Equal(DiagnosticSeverity.Warning, zati006.Severity);
        Assert.Equal(
            "[TypedId] struct 'App.Outer.Middle.Id' is not generated because its containing type 'App.Outer' is not partial",
            zati006.GetMessage(CultureInfo.InvariantCulture));
        Assert.Equal("Id", Text(source, zati006));
        Assert.Empty(run.HintNames);
        Assert.Empty(run.Errors);
    }

    [Fact]
    public void FileLocalTypedId_ReportsZati008_AndGeneratesNothing()
    {
        var source = """
            using ZeroAlloc.ValueObjects;
            namespace App;
            [TypedId] file readonly partial record struct Id;
            file partial class Outer
            {
                [TypedId] public readonly partial record struct Inner;
            }
            [TypedId] public readonly partial record struct Kept;
            """;

        var run = GeneratorRun.Of(new TypedIdGenerator(), source);

        Assert.Equal(2, run.GeneratorDiagnostics.Length);
        Assert.All(run.GeneratorDiagnostics, d =>
        {
            Assert.Equal("ZATI008", d.Id);
            Assert.Equal(DiagnosticSeverity.Error, d.Severity);
        });
        Assert.Equal(
            "[TypedId] struct 'App.Id' is not generated because 'App.Id' is file-local",
            AssertEx.One(run.GeneratorDiagnostics, d => string.Equals(Text(source, d), "Id", StringComparison.Ordinal)).GetMessage(CultureInfo.InvariantCulture));
        Assert.Equal(
            "[TypedId] struct 'App.Outer.Inner' is not generated because 'App.Outer' is file-local",
            AssertEx.One(run.GeneratorDiagnostics, d => string.Equals(Text(source, d), "Inner", StringComparison.Ordinal)).GetMessage(CultureInfo.InvariantCulture));
        Assert.Equal(["App.Kept.TypedId.g.cs"], run.HintNames);
    }

    [Fact]
    public void TypedIdsDifferingOnlyInCase_ReportZati009_OnTheLaterOne_AndGenerateTheRest()
    {
        var source = """
            using ZeroAlloc.ValueObjects;
            namespace App
            {
                [TypedId] public readonly partial record struct OrderId;
                [TypedId] public readonly partial record struct orderId;
                [TypedId] public readonly partial record struct Other;
            }
            """;

        var run = GeneratorRun.Of(new TypedIdGenerator(), source);

        var zati009 = AssertEx.One(run.GeneratorDiagnostics);
        Assert.Equal("ZATI009", zati009.Id);
        Assert.Equal(DiagnosticSeverity.Error, zati009.Severity);
        Assert.Equal(
            "[TypedId] struct 'App.orderId' is not generated because its file name 'App.orderId.TypedId.g.cs' differs only in case from that of 'App.OrderId'",
            zati009.GetMessage(CultureInfo.InvariantCulture));
        Assert.Equal("orderId", Text(source, zati009));
        Assert.Equal(["App.OrderId.TypedId.g.cs", "App.Other.TypedId.g.cs"], run.HintNames);
    }

    [Fact]
    public void EarlierTypedId_IsTheOneDeclaredFirst_AcrossFiles()
    {
        var first = CSharpSyntaxTree.ParseText(
            "using ZeroAlloc.ValueObjects; namespace App; [TypedId] public readonly partial record struct orderId;",
            path: "a.cs");
        var second = CSharpSyntaxTree.ParseText(
            "using ZeroAlloc.ValueObjects; namespace App; [TypedId] public readonly partial record struct OrderId;",
            path: "b.cs");

        var result = CSharpGeneratorDriver.Create(new TypedIdGenerator())
            .RunGenerators(TypedIdDiagnosticTests.CreateCompilation(second, first))
            .GetRunResult()
            .Results[0];

        Assert.Equal("App.orderId.TypedId.g.cs", AssertEx.One(result.GeneratedSources).HintName);
        var zati009 = AssertEx.One(result.Diagnostics, d => string.Equals(d.Id, "ZATI009", StringComparison.Ordinal));
        Assert.Equal("b.cs", zati009.Location.SourceTree!.FilePath);
    }

    private static string Text(string source, Diagnostic d) =>
        source.Substring(d.Location.SourceSpan.Start, d.Location.SourceSpan.Length);

    private static void AssertGenerated(GeneratorRun run, string metadataName)
    {
        var type = run.Output.GetTypeByMetadataName(metadataName);
        Assert.NotNull(type);
        Assert.Contains(type!.GetMembers("New"), m => m.IsStatic);
        AssertEx.One(type.GetTypeMembers("TypedIdJsonConverter"));
    }
}
