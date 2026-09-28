namespace ZeroAlloc.ValueObjects.Generator;

/// <summary>The names the TypedId generator gives its pipeline steps, so tests can check they stay cached.</summary>
internal static class TrackingNames
{
    public const string TypedIdCandidates = nameof(TypedIdCandidates);
    public const string TypedIdAssemblyDefault = nameof(TypedIdAssemblyDefault);
}
