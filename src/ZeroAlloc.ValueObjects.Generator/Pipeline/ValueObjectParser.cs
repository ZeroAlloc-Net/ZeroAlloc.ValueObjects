using System.Collections.Generic;
using System.Linq;
using System.Threading;
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

    public static ValueObjectModel? Parse(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
    {
        if (ctx.TargetSymbol is not INamedTypeSymbol typeSymbol) return null;

        var allProps = typeSymbol.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(p => p.DeclaredAccessibility == Accessibility.Public && !p.IsStatic)
            .ToList();

        // Determine opt-in vs opt-out mode
        bool hasExplicitMembers = allProps.Any(p =>
            p.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == EqualityMemberAttributeName));

        var properties = allProps
            .Where(p =>
            {
                var attrs = p.GetAttributes().Select(a => a.AttributeClass?.ToDisplayString()).ToList();
                if (hasExplicitMembers) return attrs.Contains(EqualityMemberAttributeName);
                return !attrs.Contains(IgnoreEqualityMemberAttributeName);
            })
            .Select(p => new EqualityProperty(
                p.Name,
                p.Type.ToDisplayString(),
                p.NullableAnnotation == NullableAnnotation.Annotated))
            .ToList();

        bool forceClass = ctx.Attributes.FirstOrDefault()
            ?.NamedArguments.FirstOrDefault(a => a.Key == "ForceClass").Value.Value is true;

        // Only emit as struct when the user declared a struct; never auto-promote a class to struct.
        bool isStruct = !forceClass && typeSymbol.TypeKind == TypeKind.Struct;

        return new ValueObjectModel(
            typeSymbol.ContainingNamespace.IsGlobalNamespace
                ? string.Empty
                : typeSymbol.ContainingNamespace.ToDisplayString(),
            typeSymbol.Name,
            isStruct,
            properties);
    }
}
