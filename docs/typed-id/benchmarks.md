---
id: typed-id-benchmarks
title: Benchmark Results
slug: /docs/typed-id/benchmarks
description: Measured allocations and same-run speed ratios for TypedId strategies.
sidebar_position: 30
---

# TypedId Benchmarks

Measured 2026-10-21 on an otherwise idle machine:

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
12th Gen Intel Core i9-12900HK 2.50GHz, 1 CPU, 20 logical and 14 physical cores
.NET SDK 10.0.402
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3
```

> Allocations are exact. Speed is shown as same-run ratios; absolute timings are not published.

Every benchmark ran out of process in three launches, pinned to two performance cores. Ratio is
each row's mean time relative to `Ulid_New` in the same run, with the lowest and highest of the
three launches in brackets.

## Results

| Method | Ratio | Launch range | Allocated |
|---|---:|---:|---:|
| `Ulid_New` | 1.00 | | 0 B |
| `Ulid_ToString` | 0.29 | 0.29–0.29 | 80 B |
| `Ulid_Parse` | 0.97 | 0.94–1.01 | 0 B |
| `Ulid_TryParseSpan` | 0.98 | 0.91–1.04 | 0 B |
| `Uuid7_New` | 1.82 | 1.81–1.83 | 0 B |
| `Uuid7_ToString` | 0.10 | 0.09–0.10 | 96 B |
| `Uuid7_Parse` | 0.24 | 0.24–0.24 | 0 B |
| `Snowflake_New` | 4.93 | 4.90–4.94 | 0 B |
| `Snowflake_ToString` | 0.15 | 0.14–0.16 | 64 B |
| `Snowflake_Parse` | 0.27 | 0.26–0.27 | 0 B |
| `Sequential_New` | 0.09 | 0.09–0.10 | 0 B |
| `Sequential_ToString` | below resolution ¹ | | 0 B |

¹ Formatting the small sample value takes about a nanosecond, too close to BenchmarkDotNet's resolution for a ratio.

## Observations

**Zero-allocation paths.** Every `New()`, every `Parse`, and every `TryParse(ReadOnlySpan<char>, …)` allocates 0 B. That holds for all four strategies:

- `Ulid_New`, `Uuid7_New`, `Snowflake_New`, `Sequential_New` — 0 B.
- `Ulid_Parse`, `Uuid7_Parse`, `Snowflake_Parse` — 0 B.
- `Ulid_TryParseSpan` — 0 B (the span overload round-trips without touching the heap).

This is the zero-allocation claim from the README, observed in the wild: generation and parsing of a `[TypedId]` never produces GC pressure.

**`ToString` necessarily allocates.** Any method that returns a `string` must allocate that string — there is no way to produce a 0-byte managed string. The three `*_ToString` rows show exactly the minimum allocation the string object requires for its payload:

- `Ulid_ToString` — 80 B (26-char Crockford base32).
- `Uuid7_ToString` — 96 B (36-char canonical form with dashes).
- `Snowflake_ToString` — 64 B (19-character decimal max).

`Sequential_ToString` allocates 0 B because the sample value sits in the small-integer cache that `long.ToString` returns by reference — single- and double-digit sequence values do not allocate. Once the counter grows past that cache boundary the call allocates a fresh string, identical in shape to the Snowflake row.

The generated id has no `TryFormat` of its own. To get the text into a buffer without allocating, format the underlying value: `UlidCore.TryFormatBase32(id.Value, destination, out var written)` for a ULID, `id.Value.TryFormat(destination, out var written, "D")` for a UUIDv7, and `id.Value.TryFormat(destination, out var written, default, CultureInfo.InvariantCulture)` for Snowflake and Sequential. Each writes exactly what `ToString` returns. The generated JSON converter already writes this way, so serializing an id with `System.Text.Json` does not allocate a string.

## Serialization

`TypedIdSerializationBenchmarks` drives each generated `TypedIdJsonConverter` directly, without `JsonSerializer` around it, and the generated `TypedIdSerializer` for ZeroAlloc.Serialisation. `SnowflakeEnvironmentBenchmarks` measures `New()` when the worker id comes from `ZA_SNOWFLAKE_WORKER_ID` instead of a registered provider. Measured in the same session as the table above:

| Method                           | Allocated |
|--------------------------------- |----------:|
| Json_Ulid_Read                   |       0 B |
| Json_Ulid_ReadEscaped            |       0 B |
| Json_Uuid7_Read                  |       0 B |
| Json_Snowflake_Read              |       0 B |
| Json_Ulid_Write                  |       0 B |
| Json_Uuid7_Write                 |       0 B |
| Json_Snowflake_Write             |       0 B |
| Json_Sequential_Write            |       0 B |
| Serializer_Ulid_Serialize        |       0 B |
| Serializer_Ulid_Deserialize      |       0 B |
| Serializer_Snowflake_Serialize   |       0 B |
| Serializer_Snowflake_Deserialize |       0 B |
| Snowflake_New_FromEnvironment    |       0 B |

The converter reads the JSON string into a stack buffer, or a pooled one for oversized input, and parses that span; it writes by formatting into a stack buffer. Before this change every read and write allocated the id's string: 80 B for a ULID, 96 B for a UUIDv7, 64 B for a Snowflake id. Reading the environment variable cost 24 B per Snowflake `New()`; it is now read once per id type and cached.

**Timing notes.** The benchmark calls `New()` back to back, so `Snowflake_New` measures generation at the format's ceiling: a Snowflake id has a 12-bit sequence, 4096 ids per millisecond per worker, and a call that finds the sequence used up waits for the next millisecond. That puts the floor at about 244 ns per id, which is where `Snowflake_New` and `Snowflake_New_FromEnvironment` land; at realistic call rates the sequence is never used up and generation costs a timestamp read and one compare-exchange. Until 2026-10-21 the wait slept for a timer tick, 15.6 ms on Windows, and the same row took 69 times as long as `Ulid_New`; it now takes 4.9 times as long. `Sequential_New` is a single interlocked increment.

**Validation.** The numbers confirm the README's zero-allocation guarantee for the hot paths (`New`, `Parse`, span-based `TryParse`). The only allocations in the entire suite come from `ToString`, which is an unavoidable property of returning a `string` — applications that render IDs to text on the hot path should format the underlying value into a span as described above.
