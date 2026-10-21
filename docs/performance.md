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

Measured 2026-10-21 on an otherwise idle machine:

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
12th Gen Intel Core i9-12900HK 2.50GHz, 1 CPU, 20 logical and 14 physical cores
.NET SDK 10.0.402
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3
```

> Allocations are exact. Speed is shown as same-run ratios; absolute timings are not published.

Every benchmark ran out of process in three launches, pinned to two performance cores. Ratios
come from one run, with the lowest and highest of the three launches in brackets.
CSharpFunctionalExtensions 3.7.0, Vogen 8.0.7.

### Multi-field equality (Money(decimal, string))

Each row's time relative to `ZeroAlloc_Equals`, the generated class:

| Method | Ratio | Launch range | Allocated |
|---|---:|---:|---:|
| `CFE_Equals` | 443.75 | 438.40–448.26 | 144 B |
| `Record_Equals` | 1.10 | 1.05–1.21 | 0 B |
| `RecordStruct_Equals` | 1.00 | 0.91–1.09 | 0 B |
| **`ZeroAlloc_Equals`** | **1.00** | | **0 B** |
| **`ZeroAllocStruct_Equals`** | **0.94** | 0.91–1.01 | **0 B** |

Each row's time relative to `ZeroAlloc_GetHashCode`:

| Method | Ratio | Launch range | Allocated |
|---|---:|---:|---:|
| `CFE_GetHashCode` | 0.12 ¹ | 0.11–0.15 | 0 B ¹ |
| `Record_GetHashCode` | 0.89 | 0.85–0.93 | 0 B |
| `RecordStruct_GetHashCode` | 0.45 | 0.45–0.45 | 0 B |
| **`ZeroAlloc_GetHashCode`** | **1.00** | | **0 B** |
| **`ZeroAllocStruct_GetHashCode`** | **0.86** | 0.85–0.87 | **0 B** |

¹ CSharpFunctionalExtensions caches the hash code in the instance on the first call, and the benchmark calls `GetHashCode` on the same instance every time, so this row measures reading that cache. The first call on each instance computes the hash and allocates 72 B, measured per thread with `GC.GetAllocatedBytesForCurrentThread()` over 100,000 fresh instances.

`Equals` runs at parity with `record` and `record struct` and is 444 times as fast as CSharpFunctionalExtensions', which allocates 144 B per call. `GetHashCode` takes longer than a record's: the generator emits `System.HashCode.Combine`, which mixes the fields more thoroughly than the multiply-add a record emits. On the class that costs 1.12 times a record's hash; on the struct, 1.9 times a record struct's.

### Single-int wrapped IDs vs Vogen

<!-- BENCH:START -->
_Last refreshed: 2026-10-21_

| Operation | Vogen | ZA.ValueObjects |
|---|---:|---:|
| `From(value)` | below resolution | below resolution |
| `Equals` (equal) | below resolution | below resolution |
| `Equals` (not equal) | below resolution | below resolution |
| `GetHashCode` | below resolution | below resolution |
| `ToString` | 1.00 | 1.09 (0.90–1.42) |

Both libraries allocate 0 B on every row. For `ToString` that holds only because the benchmark formats 42, and .NET caches the strings of small integers; formatting a larger value returns a new string.

Every row except `ToString` takes well under a nanosecond for both libraries, below what BenchmarkDotNet resolves: the JIT reduces wrapping, comparing or hashing a single `int` to almost nothing, so no ratio is given. `ToString` is at parity within the launch spread. The May 2026 capture reported ZeroAlloc 12–15 times faster on `From` and `Equals`; those ratios came from sub-nanosecond figures and do not hold up with three pinned launches.

**The trade-off**: on a single wrapped primitive the two libraries perform the same. Vogen's narrower wrapping (single primitive) and ZA's broader surface (multi-field, custom types, EF Core converters) make them complementary choices — pick by feature surface, not raw single-int benchmark numbers.

History: the previous single-property `ToString` allocated ~72 B per call and `GetHashCode` was ~30× slower than Vogen, both fixed in ZeroAlloc.ValueObjects v1.7 by emitting bare `Value.ToString(InvariantCulture)` / `Value.GetHashCode()` for 1-property `[ValueObject]` types. Multi-property types are unchanged.
<!-- BENCH:END -->

### Multi-field is a ZA-only feature

The biggest API difference: **Vogen wraps a single primitive**, while ZA.ValueObjects' `[ValueObject]` supports any number of fields of any types. The CFE/record/record-struct rows above show ZA at parity with the language-built-in alternatives on multi-field equality; Vogen has no equivalent surface.

## Why CFE allocates

```csharp
// Each Equals enumerates both sides: two iterator objects + two boxed decimals, 144 B.
// GetHashCode enumerates once and caches the result in the instance, 72 B on first call.
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
dotnet run -c Release --project benchmarks/ZeroAlloc.ValueObjects.Benchmarks -- --filter "*" --launchCount 3
```
