; Unshipped analyzer release.
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category                       | Severity | Notes
--------|--------------------------------|----------|---------------------------------------------------------------------
ZATI006 | ZeroAlloc.ValueObjects.TypedId | Warning  | Nested [TypedId] struct inside a containing type that is not partial
ZATI007 | ZeroAlloc.ValueObjects.TypedId | Error    | Generic [TypedId] struct
ZATI008 | ZeroAlloc.ValueObjects.TypedId | Error    | File-local [TypedId] struct
ZATI009 | ZeroAlloc.ValueObjects.TypedId | Error    | [TypedId] struct name differs only in case from another
ZAVO001 | ZeroAlloc.ValueObjects         | Warning  | Nested value object inside a containing type that is not partial
ZAVO002 | ZeroAlloc.ValueObjects         | Error    | File-local value object
ZAVO003 | ZeroAlloc.ValueObjects         | Error    | Value object name differs only in case from another value object
