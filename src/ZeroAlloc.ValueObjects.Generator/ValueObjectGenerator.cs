using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using ZeroAlloc.ValueObjects.Generator.Models;
using ZeroAlloc.ValueObjects.Generator.Pipeline;
using ZeroAlloc.ValueObjects.Generator.Writers;

namespace ZeroAlloc.ValueObjects.Generator;

[Generator]
public sealed class ValueObjectGenerator : IIncrementalGenerator
{
    private const string ValueObjectAttributeFqn = "ZeroAlloc.ValueObjects.ValueObjectAttribute";
    // EPS06 fires on every Where/Select in an incremental pipeline as of Roslyn
    // 4.14: IncrementalValuesProvider<T> grew from one instance field to two
    // (8 -> 16 bytes), crossing ErrorProne's large-struct threshold. It stayed a
    // readonly struct, so there is no defensive copy and none of the correctness
    // risk EPS06 exists to catch - only a 16-byte copy in setup code that runs
    // once per compilation, not per syntax node.
    //
    // Suppressed rather than fixed because it cannot be fixed: Where and Select
    // are extension methods on the Roslyn API taking the provider by value, with
    // no by-ref overload. Chaining them *is* the incremental generator pipeline.
    // ErrorProne.NET.Structs 0.1.2 exposes no threshold setting to correct instead.
    [SuppressMessage(
        "ErrorProne.NET.Structs",
        "EPS06:Hidden struct copy operation",
        Justification = "Roslyn's own pipeline API passes the readonly 16-byte IncrementalValuesProvider by value; there is no alternative overload.")]

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
