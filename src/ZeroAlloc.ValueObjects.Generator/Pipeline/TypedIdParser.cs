using System.Collections.Generic;
using System.Collections.Immutable;
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
    // them. File is null when the struct is not generated, for an error or for a
    // blocking warning such as ZATI006; only a struct with a File takes part in ZATI009.
    internal sealed record PartialModel(
        string HintName,
        string? Namespace,
        string Name,
        string Accessibility,
        EquatableArray<string> ContainingTypes,
        int RawStrategy,
        int RawBacking,
        EquatableArray<DiagnosticInfo> Diagnostics,
        GeneratedFile? File);

    internal sealed record AssemblyDefault(int RawStrategy, int RawBacking);

    public static PartialModel? Parse(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
    {
        if (ctx.TargetSymbol is not INamedTypeSymbol symbol) return null;
        var attr = ctx.Attributes[0];

        int strategy = ReadNamedInt(attr, "Strategy", -1);
        int backing = ReadNamedInt(attr, "Backing", 0);

        var ns = symbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : symbol.ContainingNamespace.ToDisplayString();

        // Every diagnostic is reported at the declaration that carries [TypedId], except
        // ZATI001, which goes on the attribute, and ZATI003, which goes on the member.
        var identifier = ctx.TargetNode is TypeDeclarationSyntax target
            ? LocationInfo.From(target.Identifier)
            : LocationInfo.From(ctx.TargetNode);
        var hintName = HintNames.For(symbol, ".TypedId.g.cs");
        var displayName = symbol.ToDisplayString();

        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();
        var blocked = DetectUngeneratable(symbol, identifier, displayName);
        if (blocked is not null)
        {
            diagnostics.Add(blocked);
        }
        else
        {
            DetectDeclarationIssues(symbol, identifier, diagnostics, ct);
            DetectIncompatibleBacking(attr, identifier, strategy, backing, diagnostics, ct);
        }

        // An error suppresses emission, and so does ZATI006, a warning: the containing type
        // cannot be reopened. ZATI005 does not.
        var generated = blocked is null;
        foreach (var d in diagnostics)
        {
            if (d.Severity == DiagnosticSeverity.Error) generated = false;
        }

        return new PartialModel(
            hintName,
            ns,
            symbol.Name,
            TypeDeclarations.AccessibilityModifier(symbol),
            TypeDeclarations.ContainingDeclarations(symbol),
            strategy,
            backing,
            new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable()),
            generated ? new GeneratedFile(hintName, displayName, identifier) : null);
    }

    // ZATI008 (file-local), ZATI007 (generic), ZATI006 (containing type not partial). Each
    // means nothing can be generated, so the other checks are skipped.
    private static DiagnosticInfo? DetectUngeneratable(INamedTypeSymbol symbol, LocationInfo identifier, string displayName)
    {
        if (TypeDeclarations.FileLocalType(symbol) is { } fileLocal)
            return DiagnosticInfo.Create(TypedIdDiagnostics.FileLocal, identifier, displayName, fileLocal.ToDisplayString());

        // [JsonConverter(typeof ...)] cannot name Key<T>.TypedIdJsonConverter, and
        // System.Text.Json cannot instantiate the open form Key<>.TypedIdJsonConverter.
        if (TypeDeclarations.GenericType(symbol) is { } generic)
            return DiagnosticInfo.Create(TypedIdDiagnostics.Generic, identifier, displayName, generic.ToDisplayString());

        if (TypeDeclarations.FirstNonPartialContainingType(symbol) is { } notPartial)
            return DiagnosticInfo.Create(TypedIdDiagnostics.ContainingTypeNotPartial, identifier, displayName, notPartial.ToDisplayString());

        return null;
    }

    // ZATI002 (not readonly partial record struct), ZATI003 (non-empty body), ZATI005 (multi-file partial).
    private static void DetectDeclarationIssues(
        INamedTypeSymbol symbol,
        LocationInfo identifier,
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
            diagnostics.Add(DiagnosticInfo.Create(TypedIdDiagnostics.InvalidDeclaration, identifier, symbol.Name));
        }

        if (files.Count > 1)
        {
            diagnostics.Add(DiagnosticInfo.Create(TypedIdDiagnostics.MultiFilePartial, identifier, symbol.Name));
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
                diag = DiagnosticInfo.Create(TypedIdDiagnostics.NonEmptyBody, LocationInfo.From(member), symbolName);
                return true;
            }
        }

        diag = null!;
        return false;
    }

    // ZATI001: strategy/backing compatibility. An explicit incompatible pairing on the
    // struct's attribute is always an error; assembly defaults cannot rescue an explicit
    // user-supplied pair.
    private static void DetectIncompatibleBacking(
        AttributeData attr,
        LocationInfo identifier,
        int strategy,
        int backing,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics,
        CancellationToken ct)
    {
        if (strategy < 0 || backing <= 0) return;

        var (expected, strategyName, actualName) = DescribePair(strategy, backing);
        if (expected is null) return;

        // Reported at the [TypedId] attribute that carries the incompatible pair.
        var syntax = attr.ApplicationSyntaxReference?.GetSyntax(ct);
        diagnostics.Add(DiagnosticInfo.Create(
            TypedIdDiagnostics.IncompatibleBacking,
            syntax is null ? identifier : LocationInfo.From(syntax),
            strategyName, expected, actualName));
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
            Backing: backing,
            Accessibility: partial.Accessibility,
            ContainingTypes: partial.ContainingTypes);
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
