; Unshipped analyzer release.
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### Changed Rules

Rule ID | New Category                   | New Severity | Old Category                   | Old Severity | Notes
--------|--------------------------------|--------------|--------------------------------|--------------|---------------------------------------------------------------------
ZATI006 | ZeroAlloc.ValueObjects.TypedId | Error        | ZeroAlloc.ValueObjects.TypedId | Warning      | Nested [TypedId] struct inside a containing type that is not partial
ZAVO001 | ZeroAlloc.ValueObjects         | Error        | ZeroAlloc.ValueObjects         | Warning      | Nested value object inside a containing type that is not partial
