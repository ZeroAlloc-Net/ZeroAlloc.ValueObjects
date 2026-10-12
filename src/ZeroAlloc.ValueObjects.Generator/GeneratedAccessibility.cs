using System;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ZeroAlloc.ValueObjects.Generator;

/// <summary>
/// The resolved value of the org-wide <c>ZeroAllocGeneratedAccessibility</c> MSBuild property: the
/// keyword the generator writes on the entry points it adds outside the user's own types, and the
/// raw value when it was invalid, so ZATI010 can name it. Members generated into a user's type keep
/// the accessibility of that type and do not read it.
/// </summary>
internal sealed record GeneratedAccessibility(string Keyword, string? InvalidValue)
{
    private const string PropertyKey = "build_property.ZeroAllocGeneratedAccessibility";

    /// <summary>
    /// Reads the property. Unset, empty or <c>Public</c> means <c>public</c>, <c>Internal</c> means
    /// <c>internal</c>, both ignoring case. Any other value falls back to <c>public</c> and is kept
    /// for ZATI010.
    /// </summary>
    public static GeneratedAccessibility Resolve(AnalyzerConfigOptionsProvider provider)
    {
        if (!provider.GlobalOptions.TryGetValue(PropertyKey, out var raw) ||
            string.IsNullOrEmpty(raw) ||
            string.Equals(raw, "Public", StringComparison.OrdinalIgnoreCase))
        {
            return new GeneratedAccessibility("public", null);
        }

        return string.Equals(raw, "Internal", StringComparison.OrdinalIgnoreCase)
            ? new GeneratedAccessibility("internal", null)
            : new GeneratedAccessibility("public", raw);
    }
}
