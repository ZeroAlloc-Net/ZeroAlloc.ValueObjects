namespace ZeroAlloc.ValueObjects.Generator.Models;

/// <summary>A type the generator would add a file for, as the case-collision check sees it.</summary>
internal sealed record GeneratedFile(string HintName, string DisplayName, LocationInfo Location);
