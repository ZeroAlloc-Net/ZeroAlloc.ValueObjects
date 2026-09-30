namespace ZeroAlloc.ValueObjects.Generator;

/// <summary>The names the generators give their pipeline steps, so tests can check they stay cached.</summary>
internal static class TrackingNames
{
    public const string TypedIdCandidates = nameof(TypedIdCandidates);
    public const string TypedIdAssemblyDefault = nameof(TypedIdAssemblyDefault);
    public const string TypedIdCaseCollisions = nameof(TypedIdCaseCollisions);
    public const string ValueObjectTargets = nameof(ValueObjectTargets);
    public const string ValueObjectCaseCollisions = nameof(ValueObjectCaseCollisions);
}
