using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using ZeroAlloc.ValueObjects.Generator.Models;

namespace ZeroAlloc.ValueObjects.Generator;

/// <summary>
/// The types whose hint names differ only in case from an earlier type's, which Roslyn would
/// reject as duplicates, and the errors about them. The scheme is the one ZeroAlloc.Mapping uses.
/// </summary>
internal sealed record CaseCollisions(
    EquatableArray<string> SkippedHintNames,
    EquatableArray<DiagnosticInfo> Diagnostics)
{
    /// <summary>
    /// Groups the files by hint name as Roslyn compares them, ignoring case. In each group the
    /// type declared first, by file path and then position, keeps its file; every later type is
    /// skipped and gets <paramref name="descriptor"/> on its name. The order does not depend on
    /// the order the pipeline delivers the types in, so the same type is generated on every run.
    /// </summary>
    public static CaseCollisions Find(ImmutableArray<GeneratedFile> files, DiagnosticDescriptor descriptor)
    {
        var sorted = files.ToArray();
        System.Array.Sort(sorted, CompareDeclarationOrder);

        var first = new Dictionary<string, GeneratedFile>(System.StringComparer.OrdinalIgnoreCase);
        var skipped = ImmutableArray.CreateBuilder<string>();
        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();
        foreach (var file in sorted)
        {
            if (!first.TryGetValue(file.HintName, out var earlier))
            {
                first.Add(file.HintName, file);
                continue;
            }

            skipped.Add(file.HintName);
            diagnostics.Add(DiagnosticInfo.Create(
                descriptor, file.Location, file.DisplayName, file.HintName, earlier.DisplayName));
        }

        return new CaseCollisions(
            new EquatableArray<string>(skipped.ToImmutable()),
            new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable()));
    }

    /// <summary>Whether the file with <paramref name="hintName"/> must not be added.</summary>
    public bool Skips(string hintName)
    {
        foreach (var skippedName in SkippedHintNames)
        {
            if (string.Equals(skippedName, hintName, System.StringComparison.Ordinal)) return true;
        }
        return false;
    }

    private static int CompareDeclarationOrder(GeneratedFile x, GeneratedFile y)
    {
        var byPath = string.CompareOrdinal(x.Location.Tree.FilePath, y.Location.Tree.FilePath);
        if (byPath != 0) return byPath;
        var byPosition = x.Location.Span.Start.CompareTo(y.Location.Span.Start);
        return byPosition != 0 ? byPosition : string.CompareOrdinal(x.HintName, y.HintName);
    }
}
