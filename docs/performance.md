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
_Last refreshed: 2026-05-18_

| Operation | Vogen | ZA.ValueObjects | Winner |
|---|---:|---:|---|
| `From(value)` | 4.66 ns | **0.39 ns** | **ZA 12× faster** |
| `Equals` (equal) | 1.15 ns | **0.09 ns** | **ZA 13× faster** |
| `Equals` (not equal) | 0.31 ns | **0.02 ns** | **ZA 15× faster** |
| `GetHashCode` | 0.03 ns | 0.42 ns | parity (both in BDN ZeroMeasurement zone) |
| `ToString` | 6.40 ns | **3.52 ns** | **ZA 1.8× faster** |

Both libraries are 0 B on every row. ZA wins the hot-path operations (`From`, `Equals`) by a wide margin — Vogen's `From` pays validation overhead even when validation succeeds. `GetHashCode` is effectively a tie (both rows sit in BDN's ZeroMeasurement zone — "indistinguishable from empty method"). `ToString` is now ZA's win after the single-property generator emit was aligned: the generator emits `Value.ToString(CultureInfo.InvariantCulture)` directly instead of the previous record-wrapped `$"TypeName {{ Value = {Value} }}"` interpolation.

**The trade-off**: ZA optimises construction, equality, and now `ToString` and hashing alongside Vogen. Vogen's narrower wrapping (single primitive) and ZA's broader surface (multi-field, custom types, EF Core converters) make them complementary choices — pick by feature surface, not raw single-int benchmark numbers.

History: the previous single-property `ToString` allocated ~72 B per call and `GetHashCode` was ~30× slower than Vogen, both fixed in ZeroAlloc.ValueObjects v1.7 by emitting bare `Value.ToString(InvariantCulture)` / `Value.GetHashCode()` for 1-property `[ValueObject]` types. Multi-property types are unchanged.
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
