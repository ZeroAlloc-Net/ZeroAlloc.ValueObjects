using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ZeroAlloc.ValueObjects.Generator.Models;

namespace ZeroAlloc.ValueObjects.Generator;

/// <summary>
/// The partial declarations a target's generated members are emitted into: those of its
/// containing types, outermost first, then the target's own. A port of ZeroAlloc.Mapping's
/// <c>HostDeclarations</c>.
/// </summary>
internal static class TypeDeclarations
{
    /// <summary>
    /// The outermost containing type of <paramref name="type"/> that is not declared
    /// <c>partial</c>, or null when all of them are. The generated code has to reopen every
    /// containing type, which only a partial type allows.
    /// </summary>
    public static INamedTypeSymbol? FirstNonPartialContainingType(INamedTypeSymbol type)
    {
        INamedTypeSymbol? outermost = null;
        for (var t = type.ContainingType; t is not null; t = t.ContainingType)
        {
            if (!IsPartial(t)) outermost = t;
        }
        return outermost;
    }

    /// <summary>
    /// The file-local type that <paramref name="type"/> is or is nested in, or null. A
    /// file-local type is visible only in its own file, so a generated file cannot reopen it.
    /// </summary>
    public static INamedTypeSymbol? FileLocalType(INamedTypeSymbol type)
    {
        for (var t = type; t is not null; t = t.ContainingType)
        {
            if (t.IsFileLocal) return t;
        }
        return null;
    }

    /// <summary>
    /// The type that <paramref name="type"/> is or is nested in that has type parameters, or
    /// null. The innermost one is returned.
    /// </summary>
    public static INamedTypeSymbol? GenericType(INamedTypeSymbol type)
    {
        for (var t = type; t is not null; t = t.ContainingType)
        {
            if (t.TypeParameters.Length > 0) return t;
        }
        return null;
    }

    /// <summary>
    /// The partial declaration of every containing type, outermost first, such as
    /// <c>partial struct Holder&lt;T&gt;</c>. Each carries its kind and type parameter names, and
    /// keyword names are written as verbatim identifiers. Empty for a top-level type.
    /// </summary>
    public static EquatableArray<string> ContainingDeclarations(INamedTypeSymbol type)
    {
        var chain = ImmutableArray.CreateBuilder<string>();
        for (var t = type.ContainingType; t is not null; t = t.ContainingType)
        {
            var sb = new StringBuilder();
            if (t.IsRefLikeType) sb.Append("ref ");
            sb.Append("partial ").Append(Keyword(t)).Append(' ').Append(Identifier(t.Name));
            sb.Append(TypeParameterList(t));
            chain.Insert(0, sb.ToString());
        }
        return new EquatableArray<string>(chain.ToImmutable());
    }

    /// <summary>
    /// The type parameter names of <paramref name="type"/>, as in <c>&lt;T, U&gt;</c>, or empty
    /// when it has none. A partial part may leave out constraints, and variance never appears
    /// here: an interface with a variant type parameter cannot contain types, and a value object
    /// is a class or struct.
    /// </summary>
    public static string TypeParameterList(INamedTypeSymbol type)
    {
        if (type.TypeParameters.Length == 0) return string.Empty;
        return "<" + string.Join(", ", type.TypeParameters.Select(static p => Identifier(p.Name))) + ">";
    }

    /// <summary>The declared accessibility as a modifier, or empty for a file-local type.</summary>
    public static string AccessibilityModifier(INamedTypeSymbol type) => type.DeclaredAccessibility switch
    {
        _ when type.IsFileLocal => string.Empty,
        Accessibility.Public => "public",
        Accessibility.Internal => "internal",
        Accessibility.Private => "private",
        Accessibility.Protected => "protected",
        Accessibility.ProtectedOrInternal => "protected internal",
        Accessibility.ProtectedAndInternal => "private protected",
        _ => string.Empty,
    };

    /// <summary>A name written as an identifier: a keyword gets the verbatim <c>@</c> prefix.</summary>
    public static string Identifier(string name) =>
        SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None ? "@" + name : name;

    private static bool IsPartial(INamedTypeSymbol type) =>
        type.DeclaringSyntaxReferences.Any(static r =>
            r.GetSyntax() is TypeDeclarationSyntax declaration &&
            declaration.Modifiers.Any(static m => m.IsKind(SyntaxKind.PartialKeyword)));

    private static string Keyword(INamedTypeSymbol type) => type switch
    {
        { IsRecord: true, TypeKind: TypeKind.Struct } => "record struct",
        { IsRecord: true } => "record",
        { TypeKind: TypeKind.Struct } => "struct",
        { TypeKind: TypeKind.Interface } => "interface",
        _ => "class",
    };
}
