namespace ZeroAlloc.ValueObjects.Generator.Models;

/// <summary>
/// A generated [TypedId] struct as the EF Core conventions method sees it: the hint name of its
/// file, so a struct skipped for ZATI009 can be left out, its fully qualified name, and whether
/// code at namespace level in the assembly can name it.
/// </summary>
internal sealed record TypedIdConventionEntry(string HintName, string FullyQualifiedName, bool ReachableFromAssembly);
