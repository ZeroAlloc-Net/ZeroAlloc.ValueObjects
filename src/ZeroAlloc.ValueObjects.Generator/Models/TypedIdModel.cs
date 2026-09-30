namespace ZeroAlloc.ValueObjects.Generator.Models;

// Strategy: 0=Ulid, 1=Uuid7, 2=Snowflake, 3=Sequential
// Backing: 1=Guid, 2=Int64 (0=Default is resolved before reaching the model)
// Keep Strategy/Backing as int in the model rather than referencing the runtime enum types —
// the generator cannot reference the attribute project (would cause a circular dependency).
//
// Accessibility is the struct's declared accessibility modifier, repeated on the generated part,
// and ContainingTypes the partial declarations of its containing types, outermost first.
internal sealed record TypedIdModel(
    string? Namespace,
    string Name,
    int Strategy,
    int Backing,
    string Accessibility,
    EquatableArray<string> ContainingTypes);
