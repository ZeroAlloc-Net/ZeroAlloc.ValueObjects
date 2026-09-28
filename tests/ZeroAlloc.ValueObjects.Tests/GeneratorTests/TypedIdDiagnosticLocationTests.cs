using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace ZeroAlloc.ValueObjects.Tests.GeneratorTests;

/// <summary>
/// Every ZATI diagnostic is reported at the source it is about, bound to the syntax tree, so the
/// IDE can point at it and <c>#pragma warning disable</c> can suppress one case. A source marked
/// with [| and |] gives the expected span; the markers are removed before it runs.
/// </summary>
public sealed class TypedIdDiagnosticLocationTests
{
    [Theory]
    // ZATI001: the [TypedId] attribute that carries the incompatible pair.
    [InlineData("ZATI001", """
        using ZeroAlloc.ValueObjects;
        [[|TypedId(Strategy = IdStrategy.Snowflake, Backing = BackingType.Guid)|]]
        public readonly partial record struct Bad;
        """)]
    // ZATI002: the identifier of the declaration that carries [TypedId], not the first one.
    [InlineData("ZATI002", """
        using ZeroAlloc.ValueObjects;
        public partial record struct Mutable;
        [TypedId]
        public partial record struct [|Mutable|];
        """)]
    // ZATI003: the member that makes the body non-empty.
    [InlineData("ZATI003", """
        using ZeroAlloc.ValueObjects;
        [TypedId]
        public readonly partial record struct Bad { [|public int Extra;|] }
        """)]
    public void Diagnostic_IsReportedAtTheSourceItIsAbout(string id, string marked)
    {
        var (source, span) = Parse(marked);

        var diagnostics = TypedIdDiagnosticTests.GetDiagnostics(source);

        var diagnostic = AssertEx.One(diagnostics, d => string.Equals(d.Id, id, StringComparison.Ordinal));
        AssertAt(diagnostic.Location, TypedIdDiagnosticTests.FilePath(0), span);
    }

    [Fact]
    public void ZATI005_IsReportedAtTheIdentifierOfTheDeclarationThatCarriesTypedId()
    {
        // The attributed declaration is in the second file, so it is not the first declaration.
        var (attributed, span) = Parse("""
            using ZeroAlloc.ValueObjects;
            [TypedId]
            public readonly partial record struct [|Split|];
            """);

        var diagnostics = TypedIdDiagnosticTests.GetDiagnostics(
            "public readonly partial record struct Split;", attributed);

        var diagnostic = AssertEx.One(diagnostics, d => string.Equals(d.Id, "ZATI005", StringComparison.Ordinal));
        AssertAt(diagnostic.Location, TypedIdDiagnosticTests.FilePath(1), span);
    }

    [Fact]
    public void PragmaAroundOneStruct_SuppressesThatZATI005Only()
    {
        var diagnostics = TypedIdDiagnosticTests.GetDiagnostics(
            """
            using ZeroAlloc.ValueObjects;
            #pragma warning disable ZATI005
            [TypedId]
            public readonly partial record struct Quiet;
            #pragma warning restore ZATI005
            [TypedId]
            public readonly partial record struct Loud;
            """,
            """
            public readonly partial record struct Quiet;
            public readonly partial record struct Loud;
            """);

        var zati005 = diagnostics.Where(d => string.Equals(d.Id, "ZATI005", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, zati005.Count);
        Assert.True(Single(zati005, "Quiet").IsSuppressed);
        Assert.False(Single(zati005, "Loud").IsSuppressed);
    }

    [Fact]
    public void PragmaAroundOneMember_SuppressesThatZATI003Only_WhenItsSeverityIsLowered()
    {
        // A #pragma cannot suppress an error, so the rule is lowered to a warning, as an
        // .editorconfig would. The pragma then applies only if the diagnostic is bound to the tree.
        var diagnostics = TypedIdDiagnosticTests.GetDiagnostics(
            new Dictionary<string, ReportDiagnostic>(StringComparer.Ordinal) { ["ZATI003"] = ReportDiagnostic.Warn },
            """
            using ZeroAlloc.ValueObjects;
            [TypedId]
            public readonly partial record struct Quiet
            {
            #pragma warning disable ZATI003
                public int Extra;
            #pragma warning restore ZATI003
            }
            [TypedId]
            public readonly partial record struct Loud { public int Extra; }
            """);

        var zati003 = diagnostics.Where(d => string.Equals(d.Id, "ZATI003", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, zati003.Count);
        Assert.True(Single(zati003, "Quiet").IsSuppressed);
        Assert.False(Single(zati003, "Loud").IsSuppressed);
    }

    private static Diagnostic Single(List<Diagnostic> diagnostics, string typeName) =>
        AssertEx.One(diagnostics, d => d.GetMessage(CultureInfo.InvariantCulture)
            .Contains("'" + typeName + "'", StringComparison.Ordinal));

    private static void AssertAt(Location location, string filePath, TextSpan expected)
    {
        // A source location, bound to the tree, is what #pragma and the IDE need.
        Assert.Equal(LocationKind.SourceFile, location.Kind);
        Assert.NotNull(location.SourceTree);
        Assert.Equal(filePath, location.SourceTree!.FilePath);
        Assert.Equal(expected, location.SourceSpan);
    }

    private static (string Source, TextSpan Span) Parse(string marked)
    {
        var start = marked.IndexOf("[|", StringComparison.Ordinal);
        var end = marked.IndexOf("|]", StringComparison.Ordinal) - 2;
        var source = marked.Replace("[|", string.Empty, StringComparison.Ordinal)
            .Replace("|]", string.Empty, StringComparison.Ordinal);
        return (source, TextSpan.FromBounds(start, end));
    }
}
