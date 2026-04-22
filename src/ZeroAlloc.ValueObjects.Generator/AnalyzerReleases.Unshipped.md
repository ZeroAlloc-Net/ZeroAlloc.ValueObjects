; Unshipped analyzer release.
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
ZATI001 | ZeroAlloc.ValueObjects.TypedId | Error | Incompatible strategy/backing combination on [TypedId]
ZATI002 | ZeroAlloc.ValueObjects.TypedId | Error | [TypedId] target must be readonly partial record struct
ZATI003 | ZeroAlloc.ValueObjects.TypedId | Error | [TypedId] struct body must be empty
ZATI005 | ZeroAlloc.ValueObjects.TypedId | Warning | [TypedId] struct declared across multiple files
