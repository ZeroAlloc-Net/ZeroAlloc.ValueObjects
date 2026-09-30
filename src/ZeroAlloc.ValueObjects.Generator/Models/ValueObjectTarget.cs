namespace ZeroAlloc.ValueObjects.Generator.Models;

/// <summary>
/// What the parser found for one <c>[ValueObject]</c> type: the model to generate, when it can be
/// generated, and the diagnostics about it.
/// </summary>
internal sealed record ValueObjectTarget(
    ValueObjectModel? Model,
    EquatableArray<DiagnosticInfo> Diagnostics,
    GeneratedFile? File);
