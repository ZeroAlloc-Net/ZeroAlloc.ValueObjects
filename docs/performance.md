---
id: performance
title: Performance
slug: /docs/performance
description: Benchmark results comparing ZeroAlloc.ValueObjects against record, record struct, and CSharpFunctionalExtensions.
sidebar_position: 12
---

# Performance

## Benchmark results

ZeroAlloc.ValueObjects is benchmarked against two distinct competitor sets:

1. **Multi-field value objects** — `record`, `record struct`, and `CSharpFunctionalExtensions.ValueObject` on a two-property `Money(decimal Amount, string Currency)` type.
2. **Single-primitive wrapped IDs** — `Vogen` (the other source-generator value-object library) on a single-int wrapped type — the surface Vogen is designed for.

Benchmarks run with BenchmarkDotNet v0.13.12, .NET 9.0.15, on Windows 11.

### Multi-field equality (Money(decimal, string))

| Method | Mean | Allocated |
|---|---:|---:|
| `CFE_Equals` | 45.2 ns | 96 B |
| `Record_Equals` | 3.1 ns | 0 B |
| `RecordStruct_Equals` | 2.8 ns | 0 B |
| **`ZeroAlloc_Equals`** | **3.1 ns** | **0 B** |
| **`ZeroAllocStruct_Equals`** | **2.8 ns** | **0 B** |
| `CFE_GetHashCode` | 38.7 ns | 88 B |
| `Record_GetHashCode` | 2.4 ns | 0 B |
| `RecordStruct_GetHashCode` | 2.2 ns | 0 B |
| **`ZeroAlloc_GetHashCode`** | **2.4 ns** | **0 B** |
| **`ZeroAllocStruct_GetHashCode`** | **2.2 ns** | **0 B** |

ZA.ValueObjects matches `record` / `record struct` exactly. CFE allocates ~90 B per equality call.

### Single-int wrapped IDs vs Vogen

<!-- BENCH:START -->
_Last refreshed: 2026-05-13_

| Operation | Vogen | ZA.ValueObjects | Winner |
|---|---:|---:|---|
| `From(value)` | 4.12 ns | **0.30 ns** | **ZA 14× faster** |
| `Equals` (equal) | 0.54 ns | **0.08 ns** | **ZA 7× faster** |
| `Equals` (not equal) | 0.67 ns | **0.20 ns** | **ZA 3× faster** |
| `GetHashCode` | **0.05 ns** | 1.50 ns | Vogen 30× faster |
| `ToString` | **4.45 ns** | 41.75 ns / **72 B** | Vogen 9× faster; ZA allocates |

Both libraries are 0 B on equality and construction. ZA wins the hot-path operations (`From`, `Equals`) by a wide margin — Vogen's `From` pays validation overhead even when the validation succeeds. Vogen wins `GetHashCode` (its primitive-wrapped hash inlines to the raw int) and `ToString` (no allocation; ZA's default `ToString` boxes through string formatting and allocates 72 B).

**The trade-off**: ZA optimises construction and equality; Vogen optimises hashing and formatting. For value-object usage dominated by lookup-key equality (dictionary keys, set membership, change tracking), ZA wins. For value-object usage dominated by logging and display, Vogen wins.

ZA's `ToString` allocation is a known cost; it can be eliminated by overriding `ToString()` manually with a direct `value.ToString(CultureInfo.InvariantCulture)`.
<!-- BENCH:END -->

### Multi-field is a ZA-only feature

The biggest API difference: **Vogen wraps a single primitive**, while ZA.ValueObjects' `[ValueObject]` supports any number of fields of any types. The CFE/record/record-struct row above shows ZA at parity with the language-built-in alternatives for multi-field types; Vogen has no equivalent surface.

## Why CFE allocates

```csharp
// Each call allocates: iterator state machine + boxed value types
protected override IEnumerable<IComparable> GetEqualityComponents()
{
    yield return Amount;    // decimal → boxed object  (heap)
    yield return Currency;
}
// Iterator object itself also allocated on heap
```

## Why ZeroAlloc is zero-allocation

```csharp
// Generated — no heap objects created
public bool Equals(Money? other) =>
    other is not null &&
    Amount == other.Amount &&       // direct decimal comparison
    Currency == other.Currency;     // direct string comparison

public override int GetHashCode() =>
    System.HashCode.Combine(Amount, Currency);  // stack-only
```

## Running benchmarks

```
dotnet run -c Release --project benchmarks/ZeroAlloc.ValueObjects.Benchmarks
```
