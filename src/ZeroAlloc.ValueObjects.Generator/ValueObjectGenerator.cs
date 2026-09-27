using Microsoft.CodeAnalysis;
using ZeroAlloc.ValueObjects.Generator.Models;
using ZeroAlloc.ValueObjects.Generator.Pipeline;
using ZeroAlloc.ValueObjects.Generator.Writers;

namespace ZeroAlloc.ValueObjects.Generator;

[Generator]
public sealed class ValueObjectGenerator : IIncrementalGenerator
{
    private const string ValueObjectAttributeFqn = "ZeroAlloc.ValueObjects.ValueObjectAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var models = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                ValueObjectAttributeFqn,
                predicate: (node, _) => ValueObjectParser.IsValueObjectCandidate(node),
                transform: ValueObjectParser.Parse)
            .Where(m => m is not null)
            .Select((m, _) => m!);

        context.RegisterSourceOutput(models, Emit);
    }

    private static void Emit(SourceProductionContext ctx, ValueObjectModel model)
    {
        var source = SourceWriter.Write(model);
        var hintName = string.IsNullOrEmpty(model.Namespace)
            ? $"{model.TypeName}.g.cs"
            : $"{model.Namespace}_{model.TypeName}.g.cs";
        ctx.AddSource(hintName, source);
    }
}
