using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ZeroAlloc.ValueObjects.Generator.Models;

namespace ZeroAlloc.ValueObjects.Generator.Pipeline;

internal static class TypedIdParser
{
    // "Partial" model: values as seen on the struct's [TypedId] attribute (may be 0 = unset).
    // Full resolution happens in Resolve() combining with the assembly-level default.
    // Carries any diagnostics detected while parsing so the source-output stage can report
    // them and skip emission when errors are present.
    internal sealed record PartialModel(
        string? Namespace,
        string Name,
        int RawStrategy,
        int RawBacking,
        ImmutableArray<DiagnosticInfo> Diagnostics);

    internal sealed record AssemblyDefault(int RawStrategy, int RawBacking);

    // A value-type-ish holder for a diagnostic so the incremental pipeline can cache models
    // without pulling Location objects that compare by reference.
    internal sealed record DiagnosticInfo(
        string Id,
        DiagnosticSeverity Severity,
        LocationInfo Location,
        ImmutableArray<string> MessageArgs)
    {
        public Diagnostic ToDiagnostic()
        {
            var descriptor = Id switch
            {
                "ZATI001" => TypedIdDiagnostics.IncompatibleBacking,
                "ZATI002" => TypedIdDiagnostics.InvalidDeclaration,
                "ZATI003" => TypedIdDiagnostics.NonEmptyBody,
                "ZATI005" => TypedIdDiagnostics.MultiFilePartial,
                _ => throw new System.InvalidOperationException($"Unknown diagnostic id {Id}"),
            };
            return Diagnostic.Create(descriptor, Location.ToLocation(), MessageArgs.ToArray());
        }
    }

    internal sealed record LocationInfo(string FilePath, TextSpanInfo Span, LinePositionSpanInfo LineSpan)
    {
        public Location ToLocation() => Microsoft.CodeAnalysis.Location.Create(
            FilePath,
            new Microsoft.CodeAnalysis.Text.TextSpan(Span.Start, Span.Length),
            new Microsoft.CodeAnalysis.Text.LinePositionSpan(
                new Microsoft.CodeAnalysis.Text.LinePosition(LineSpan.StartLine, LineSpan.StartCharacter),
                new Microsoft.CodeAnalysis.Text.LinePosition(LineSpan.EndLine, LineSpan.EndCharacter)));

        public static LocationInfo From(Location location)
        {
            var lineSpan = location.GetLineSpan();
            return new LocationInfo(
                location.SourceTree?.FilePath ?? string.Empty,
                new TextSpanInfo(location.SourceSpan.Start, location.SourceSpan.Length),
                new LinePositionSpanInfo(
                    lineSpan.StartLinePosition.Line,
                    lineSpan.StartLinePosition.Character,
                    lineSpan.EndLinePosition.Line,
                    lineSpan.EndLinePosition.Character));
        }
    }

    [StructLayout(LayoutKind.Auto)]
    internal readonly record struct TextSpanInfo(int Start, int Length);

    [StructLayout(LayoutKind.Auto)]
    internal readonly record struct LinePositionSpanInfo(int StartLine, int StartCharacter, int EndLine, int EndCharacter);

    public static PartialModel? Parse(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
    {
        if (ctx.TargetSymbol is not INamedTypeSymbol symbol) return null;
        var attr = ctx.Attributes[0];

        int strategy = ReadNamedInt(attr, "Strategy", -1);
        int backing = ReadNamedInt(attr, "Backing", 0);

        var ns = symbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : symbol.ContainingNamespace.ToDisplayString();

        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();
        DetectDeclarationIssues(symbol, diagnostics, ct);
        DetectIncompatibleBacking(symbol, attr, strategy, backing, diagnostics, ct);

        return new PartialModel(ns, symbol.Name, strategy, backing, diagnostics.ToImmutable());
    }

    // ZATI002 (not readonly partial record struct), ZATI003 (non-empty body), ZATI005 (multi-file partial).
    private static void DetectDeclarationIssues(
        INamedTypeSymbol symbol,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics,
        CancellationToken ct)
    {
        var declarations = symbol.DeclaringSyntaxReferences;
        if (declarations.Length == 0) return;

        bool anyValid = false;
        bool bodyIssueReported = false;
        var files = new HashSet<string>(System.StringComparer.Ordinal);

        foreach (var declRef in declarations)
        {
            ct.ThrowIfCancellationRequested();
            var node = declRef.GetSyntax(ct);
            if (node is not TypeDeclarationSyntax typeDecl) continue;

            files.Add(declRef.SyntaxTree.FilePath ?? string.Empty);

            if (IsValidTypedIdDeclaration(typeDecl)) anyValid = true;

            if (!bodyIssueReported && TryFindBodyIssue(typeDecl, symbol.Name, out var bodyDiag))
            {
                diagnostics.Add(bodyDiag);
                bodyIssueReported = true;
            }
        }

        if (!anyValid)
        {
            diagnostics.Add(new DiagnosticInfo(
                "ZATI002",
                DiagnosticSeverity.Error,
                LocationInfo.From(GetIdentifierLocation(declarations[0], ct)),
                ImmutableArray.Create(symbol.Name)));
        }

        if (files.Count > 1)
        {
            diagnostics.Add(new DiagnosticInfo(
                "ZATI005",
                DiagnosticSeverity.Warning,
                LocationInfo.From(GetIdentifierLocation(declarations[0], ct)),
                ImmutableArray.Create(symbol.Name)));
        }
    }

    private static bool IsValidTypedIdDeclaration(TypeDeclarationSyntax typeDecl)
    {
        bool isRecord = typeDecl is RecordDeclarationSyntax rec
            && rec.ClassOrStructKeyword.RawKind == (int)SyntaxKind.StructKeyword;
        return isRecord
            && HasModifier(typeDecl, SyntaxKind.ReadOnlyKeyword)
            && HasModifier(typeDecl, SyntaxKind.PartialKeyword);
    }

    private static bool TryFindBodyIssue(TypeDeclarationSyntax typeDecl, string symbolName, out DiagnosticInfo diag)
    {
        foreach (var member in typeDecl.Members)
        {
            if (member is FieldDeclarationSyntax || member is PropertyDeclarationSyntax)
            {
                diag = new DiagnosticInfo(
                    "ZATI003",
                    DiagnosticSeverity.Error,
                    LocationInfo.From(member.GetLocation()),
                    ImmutableArray.Create(symbolName));
                return true;
            }
        }

        diag = null!;
        return false;
    }

    private static Location GetIdentifierLocation(SyntaxReference declRef, CancellationToken ct)
    {
        var node = declRef.GetSyntax(ct);
        return node is TypeDeclarationSyntax td ? td.Identifier.GetLocation() : node.GetLocation();
    }

    // ZATI001: strategy/backing compatibility. An explicit incompatible pairing on the
    // struct's attribute is always an error; assembly defaults cannot rescue an explicit
    // user-supplied pair.
    private static void DetectIncompatibleBacking(
        INamedTypeSymbol symbol,
        AttributeData attr,
        int strategy,
        int backing,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics,
        CancellationToken ct)
    {
        if (strategy < 0 || backing <= 0) return;

        var (expected, strategyName, actualName) = DescribePair(strategy, backing);
        if (expected is null) return;

        Location loc = attr.ApplicationSyntaxReference?.GetSyntax(ct).GetLocation()
            ?? symbol.Locations[0];
        diagnostics.Add(new DiagnosticInfo(
            "ZATI001",
            DiagnosticSeverity.Error,
            LocationInfo.From(loc),
            ImmutableArray.Create(strategyName, expected, actualName)));
    }

    private static bool HasModifier(TypeDeclarationSyntax typeDecl, SyntaxKind kind)
    {
        foreach (var mod in typeDecl.Modifiers)
        {
            if (mod.RawKind == (int)kind) return true;
        }
        return false;
    }

    // Returns (expectedBackingName, strategyName, actualBackingName) when the pair is
    // incompatible; (null, _, _) when compatible.
    private static (string? expected, string strategyName, string actualName) DescribePair(int strategy, int backing)
    {
        // Strategy: 0=Ulid, 1=Uuid7, 2=Snowflake, 3=Sequential
        // Backing:  1=Guid, 2=Int64
        string strategyName = strategy switch
        {
            0 => "Ulid",
            1 => "Uuid7",
            2 => "Snowflake",
            3 => "Sequential",
            _ => strategy.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        string backingName = backing switch
        {
            1 => "Guid",
            2 => "Int64",
            _ => backing.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };

        if ((strategy == 2 || strategy == 3) && backing != 2)
            return ("Int64", strategyName, backingName);
        if ((strategy == 0 || strategy == 1) && backing != 1)
            return ("Guid", strategyName, backingName);

        return (null, strategyName, backingName);
    }

    public static AssemblyDefault ReadAssemblyDefault(Compilation compilation)
    {
        var defaultAttrSymbol = compilation.GetTypeByMetadataName("ZeroAlloc.ValueObjects.TypedIdDefaultAttribute");
        if (defaultAttrSymbol is null) return new AssemblyDefault(-1, 0);

        foreach (var attr in compilation.Assembly.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, defaultAttrSymbol))
            {
                int strategy = ReadNamedInt(attr, "Strategy", -1);
                int backing = ReadNamedInt(attr, "Backing", 0);
                return new AssemblyDefault(strategy, backing);
            }
        }

        return new AssemblyDefault(-1, 0);
    }

    public static TypedIdModel Resolve(PartialModel partial, AssemblyDefault asmDefault)
    {
        // Strategy: per-struct → assembly default → Ulid (0)
        int strategy = partial.RawStrategy >= 0
            ? partial.RawStrategy
            : (asmDefault.RawStrategy >= 0 ? asmDefault.RawStrategy : 0);

        // Backing: per-struct → assembly default → auto (1=Guid for Ulid/Uuid7, 2=Int64 for Snowflake/Sequential)
        int backing = partial.RawBacking > 0
            ? partial.RawBacking
            : (asmDefault.RawBacking > 0 ? asmDefault.RawBacking : AutoBacking(strategy));

        return new TypedIdModel(
            Namespace: partial.Namespace,
            Name: partial.Name,
            Strategy: strategy,
            Backing: backing);
    }

    private static int AutoBacking(int strategy) => strategy switch
    {
        0 or 1 => 1,   // Ulid, Uuid7 → Guid
        2 or 3 => 2,   // Snowflake, Sequential → Int64
        _ => 1,
    };

    private static int ReadNamedInt(AttributeData attr, string name, int defaultValue)
    {
        foreach (var kv in attr.NamedArguments)
        {
            if (string.Equals(kv.Key, name, System.StringComparison.Ordinal) && kv.Value.Value is int v)
                return v;
        }
        return defaultValue;
    }
}
