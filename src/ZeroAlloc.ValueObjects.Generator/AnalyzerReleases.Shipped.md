; Shipped analyzer releases.
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 1.2.0

### New Rules

Rule ID | Category                       | Severity | Notes
--------|--------------------------------|----------|---------------------------------------------------------
ZATI001 | ZeroAlloc.ValueObjects.TypedId | Error    | Incompatible strategy/backing combination on [TypedId]
ZATI002 | ZeroAlloc.ValueObjects.TypedId | Error    | [TypedId] target must be readonly partial record struct
ZATI003 | ZeroAlloc.ValueObjects.TypedId | Error    | [TypedId] struct body must be empty
ZATI005 | ZeroAlloc.ValueObjects.TypedId | Warning  | [TypedId] struct declared across multiple files

## Release 2.0.12

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
