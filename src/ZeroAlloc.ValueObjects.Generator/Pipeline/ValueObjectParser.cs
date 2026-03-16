using System;
using System.Collections.Generic;
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

    public static ValueObjectModel? Parse(GeneratorAttributeSyntaxContext ctx, System.Threading.CancellationToken _)
    {
        if (ctx.TargetSymbol is not INamedTypeSymbol typeSymbol) return null;

        var properties = ResolveProperties(typeSymbol);
        bool forceClass = DetectForceClass(ctx.TargetNode);
        bool isStruct = !forceClass && typeSymbol.TypeKind == TypeKind.Struct;

        return new ValueObjectModel(
            typeSymbol.ContainingNamespace.IsGlobalNamespace
                ? string.Empty
                : typeSymbol.ContainingNamespace.ToDisplayString(),
            typeSymbol.Name,
            isStruct,
            properties);
    }

    private static IReadOnlyList<EqualityProperty> ResolveProperties(INamedTypeSymbol typeSymbol)
    {
        var allProps = typeSymbol.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(p => p.DeclaredAccessibility == Accessibility.Public && !p.IsStatic)
            .ToList();

        bool hasExplicitMembers = allProps.Any(p =>
            p.GetAttributes().Any(a => string.Equals(
                a.AttributeClass?.ToDisplayString(), EqualityMemberAttributeName, StringComparison.Ordinal)));

        return allProps
            .Where(p =>
            {
                var attrs = p.GetAttributes()
                    .Select(a => a.AttributeClass?.ToDisplayString())
                    .ToList();
                return hasExplicitMembers
                    ? attrs.Any(a => string.Equals(a, EqualityMemberAttributeName, StringComparison.Ordinal))
                    : !attrs.Any(a => string.Equals(a, IgnoreEqualityMemberAttributeName, StringComparison.Ordinal));
            })
            .Select(p => new EqualityProperty(
                p.Name,
                p.Type.ToDisplayString(),
                p.NullableAnnotation == NullableAnnotation.Annotated))
            .ToList();
    }

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
