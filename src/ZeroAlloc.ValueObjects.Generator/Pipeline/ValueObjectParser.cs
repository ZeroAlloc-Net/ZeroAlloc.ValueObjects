using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ZeroAlloc.ValueObjects.Generator.Models;

namespace ZeroAlloc.ValueObjects.Generator.Pipeline;

internal static class ValueObjectParser
{
    private const string EqualityMemberAttributeName = "ZeroAlloc.ValueObjects.EqualityMemberAttribute";
    private const string IgnoreEqualityMemberAttributeName = "ZeroAlloc.ValueObjects.IgnoreEqualityMemberAttribute";

    public static bool IsValueObjectCandidate(SyntaxNode node) =>
        node is TypeDeclarationSyntax { AttributeLists.Count: > 0 };

    public static ValueObjectTarget? Parse(GeneratorAttributeSyntaxContext ctx, System.Threading.CancellationToken _)
    {
        if (ctx.TargetSymbol is not INamedTypeSymbol typeSymbol) return null;

        var location = ctx.TargetNode is TypeDeclarationSyntax declaration
            ? LocationInfo.From(declaration.Identifier)
            : LocationInfo.From(ctx.TargetNode);
        var displayName = typeSymbol.ToDisplayString();

        // ZAVO002: a generated file cannot reopen a type that is visible only in its own file.
        if (TypeDeclarations.FileLocalType(typeSymbol) is { } fileLocal)
        {
            return Blocked(DiagnosticInfo.Create(
                ValueObjectDiagnostics.FileLocal, location, displayName, fileLocal.ToDisplayString()));
        }

        // ZAVO001: the generated code reopens every containing type, so each must be partial.
        if (TypeDeclarations.FirstNonPartialContainingType(typeSymbol) is { } notPartial)
        {
            return Blocked(DiagnosticInfo.Create(
                ValueObjectDiagnostics.ContainingTypeNotPartial, location, displayName, notPartial.ToDisplayString()));
        }

        var properties = ResolveProperties(typeSymbol);
        bool forceClass = DetectForceClass(ctx.TargetNode);
        bool isStruct = !forceClass && typeSymbol.TypeKind == TypeKind.Struct;
        var hintName = HintNames.For(typeSymbol, ".g.cs");

        var model = new ValueObjectModel(
            hintName,
            typeSymbol.ContainingNamespace.IsGlobalNamespace
                ? string.Empty
                : typeSymbol.ContainingNamespace.ToDisplayString(),
            typeSymbol.Name,
            isStruct,
            properties,
            TypeDeclarations.TypeParameterList(typeSymbol),
            TypeDeclarations.ContainingDeclarations(typeSymbol));
        return new ValueObjectTarget(model, default, new GeneratedFile(hintName, displayName, location));
    }

    private static ValueObjectTarget Blocked(DiagnosticInfo diagnostic) =>
        new(null, new EquatableArray<DiagnosticInfo>(ImmutableArray.Create(diagnostic)), null);

    private static EquatableArray<EqualityProperty> ResolveProperties(INamedTypeSymbol typeSymbol)
    {
        // Every instance property, not just the public ones. The accessibility
        // filter used to run here, before hasExplicitMembers was computed, which
        // dropped [EqualityMember] from non-public members silently and in two
        // different ways: a type with some public marks quietly compared a
        // narrower set than its author wrote, and a type whose marks were all
        // non-public fell through to the implicit path and compared raw public
        // properties instead -- a completely different equality. Neither produced
        // a warning. Normalising through a private helper property is a natural
        // thing to write, so this was reachable from ordinary code.
        //
        // Generated equality is emitted into the same partial type, so a private
        // member is accessible from it.
        var allProps = typeSymbol.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(p => !p.IsStatic)
            .ToList();

        bool hasExplicitMembers = allProps.Any(p =>
            p.GetAttributes().Any(a => string.Equals(
                a.AttributeClass?.ToDisplayString(), EqualityMemberAttributeName, StringComparison.Ordinal)));

        return new EquatableArray<EqualityProperty>(allProps
            .Where(p =>
            {
                var attrs = p.GetAttributes()
                    .Select(a => a.AttributeClass?.ToDisplayString())
                    .ToList();
                // Explicit opt-in honours whatever the author marked, at any
                // accessibility -- the attribute is a statement of intent.
                // Without marks the implicit set stays public-only, since
                // silently folding private state into equality would be its own
                // surprise.
                return hasExplicitMembers
                    ? attrs.Any(a => string.Equals(a, EqualityMemberAttributeName, StringComparison.Ordinal))
                    : p.DeclaredAccessibility == Accessibility.Public
                      && !attrs.Any(a => string.Equals(a, IgnoreEqualityMemberAttributeName, StringComparison.Ordinal));
            })
            .Select(p => new EqualityProperty(
                p.Name,
                p.Type.ToDisplayString(),
                p.NullableAnnotation == NullableAnnotation.Annotated,
                IsTypeParameter(p.Type)))
            .ToImmutableArray());
    }

    /// <summary>
    /// A type parameter, or a nullable value type over one: <c>==</c> is defined for neither
    /// unless a constraint provides it, so the member compares through
    /// <c>EqualityComparer&lt;T&gt;.Default</c>.
    /// </summary>
    private static bool IsTypeParameter(ITypeSymbol type) =>
        type is ITypeParameterSymbol
        || type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
           && nullable.TypeArguments[0] is ITypeParameterSymbol;

    private static bool DetectForceClass(SyntaxNode? targetNode)
    {
        if (targetNode is not TypeDeclarationSyntax typeSyntax) return false;

        foreach (var attrList in typeSyntax.AttributeLists)
        {
            foreach (var a in attrList.Attributes)
            {
                if (a.ArgumentList == null) continue;

                var attrName = a.Name.ToString();
                if (!string.Equals(attrName, "ValueObject", StringComparison.Ordinal) &&
                    !string.Equals(attrName, "ValueObjectAttribute", StringComparison.Ordinal)) continue;

                foreach (var arg in a.ArgumentList.Arguments)
                {
                    if (string.Equals(arg.NameEquals?.Name.Identifier.Text, "ForceClass", StringComparison.Ordinal) &&
                        string.Equals(arg.Expression.ToString(), "true", StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }
}
