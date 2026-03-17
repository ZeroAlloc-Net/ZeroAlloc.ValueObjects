---
id: performance
title: Performance
slug: /docs/performance
description: Benchmark results comparing ZeroAlloc.ValueObjects against record, record struct, and CSharpFunctionalExtensions.
sidebar_position: 12
---

# Performance

## Benchmark results

Benchmarks compare `ZeroAlloc.ValueObjects` against `CSharpFunctionalExtensions.ValueObject`, C# `record`, and `record struct` using a two-property type (`decimal Amount`, `string Currency`). Run with BenchmarkDotNet and `[MemoryDiagnoser]`.

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

`ZeroAlloc.ValueObjects` matches `record` and `record struct` exactly. The CFE baseline allocates ~90 bytes per equality call.

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
