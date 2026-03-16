# ZeroAlloc.ValueObjects — Design Document

**Date:** 2026-03-15
**Status:** Approved

## Problem

`CSharpFunctionalExtensions.ValueObject` uses `IEnumerable<object> GetEqualityComponents()` for equality.
Every call to `Equals()` or `GetHashCode()` allocates:
- An iterator state machine (heap)
- Boxing for every value-type property (`int`, `Guid`, `decimal`, `enum` → `object`)

In hot paths — dictionary keys, HashSets, LINQ grouping, large collections — this creates significant GC pressure.

## Solution

`ZeroAlloc.ValueObjects` — a Roslyn source generator that emits direct, zero-allocation `Equals` / `GetHashCode` implementations. No base class. No boxing. No iterator.

## User API

```csharp
// Multi-property → sealed class
[ValueObject]
public partial class Money
{
    public decimal Amount { get; }
    public string Currency { get; }
}

// Single value-type property → readonly struct (stack allocated)
[ValueObject]
public partial struct EmailAddress
{
    public string Value { get; }
}

// Explicit member selection
[ValueObject]
public partial class Address
{
    [EqualityMember] public string Street { get; }
    [EqualityMember] public string City { get; }
    public string Notes { get; }  // excluded from equality
}
```

### Generated output per type

- `bool Equals(object obj)` — direct type check + member comparison, no boxing
- `bool Equals(T other)` — `IEquatable<T>` fast path
- `int GetHashCode()` — `HashCode.Combine(...)` for ≤8 props, incremental `HashCode.Add()` for 9+
- `operator ==` / `operator !=`
- `string ToString()` — `"Money { Amount = 10, Currency = USD }"`

## Attribute Behavior

| Attribute | Target | Purpose |
|-----------|--------|---------|
| `[ValueObject]` | `partial class` or `partial struct` | Triggers generation |
| `[ValueObject(ForceClass = true)]` | `partial struct` | Override auto struct decision |
| `[EqualityMember]` | Property | Opt-in mode: only marked props participate |
| `[IgnoreEqualityMember]` | Property | Opt-out mode: exclude this prop |

**Auto struct decision:** generator emits `readonly struct` when the type has exactly 1 property of a value type (`int`, `decimal`, `Guid`, `enum`, etc.). Everything else → `sealed class`.

**Default member selection:** all `public` properties with a getter, unless any property has `[EqualityMember]` (switches to opt-in mode).

## Package Structure

```
ZeroAlloc.ValueObjects/
├── ZeroAlloc.ValueObjects/              ← attributes only, netstandard2.0
│   ├── ValueObjectAttribute.cs
│   ├── EqualityMemberAttribute.cs
│   └── IgnoreEqualityMemberAttribute.cs
├── ZeroAlloc.ValueObjects.Generator/    ← Roslyn ISourceGenerator, netstandard2.0
│   ├── ValueObjectGenerator.cs
│   ├── EqualsWriter.cs
│   └── HashCodeWriter.cs
└── ZeroAlloc.ValueObjects.Tests/
    ├── GeneratorTests/                  ← Verify snapshot tests
    └── Benchmarks/                      ← BenchmarkDotNet vs CSharpFunctionalExtensions
```

**NuGet packages:**
- `ZeroAlloc.ValueObjects` — user-facing, pulls in generator as analyzer (`PrivateAssets="all"`)
- `ZeroAlloc.ValueObjects.Generator` — internal transport only

**Runtime dependencies:** none. Attributes are embedded, no runtime DLL required.

## Success Criteria

- Zero allocations on `Equals` and `GetHashCode` for value-type and string properties
- Generator handles: nested value objects, nullable properties, `init`-only setters, record-style properties
- Snapshot tests (Verify) cover all input shapes
- BenchmarkDotNet results show ~10-15x perf improvement and 0 B allocated vs CSharpFunctionalExtensions baseline

## Out of Scope (v1)

- `IComparable<T>` generation
- JSON / EF Core integration
- Validation / factory methods
- Entity / AggregateRoot support
- Result / Maybe source gen
