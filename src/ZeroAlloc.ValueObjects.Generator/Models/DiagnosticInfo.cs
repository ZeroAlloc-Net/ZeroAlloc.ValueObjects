using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace ZeroAlloc.ValueObjects.Generator.Models;

/// <summary>
/// An equatable holder for a diagnostic, so the incremental pipeline can cache the models that
/// carry one: a <see cref="Diagnostic"/> compares by reference. The descriptors are static
/// instances and compare by value, and the location keeps its syntax tree, see
/// <see cref="LocationInfo"/>.
/// </summary>
internal sealed record DiagnosticInfo(
    DiagnosticDescriptor Descriptor,
    LocationInfo Location,
    EquatableArray<string> MessageArgs)
{
    public DiagnosticSeverity Severity => Descriptor.DefaultSeverity;

    public static DiagnosticInfo Create(DiagnosticDescriptor descriptor, LocationInfo location, params string[] messageArgs) =>
        new(descriptor, location, new EquatableArray<string>(ImmutableArray.Create(messageArgs)));

    public Diagnostic ToDiagnostic() =>
        Diagnostic.Create(Descriptor, Location.ToLocation(), MessageArgs.ToArray());
}
