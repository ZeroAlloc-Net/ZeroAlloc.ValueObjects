---
id: typed-id-benchmarks
title: Benchmark Results
slug: /docs/typed-id/benchmarks
description: Measured allocation and timing numbers for TypedId strategies.
sidebar_position: 30
---

# TypedId Benchmarks

Measured on `marcel-laptop` with BenchmarkDotNet v0.13.12 on 2026-04-22. `[MemoryDiagnoser]` reports allocated bytes per call. Numbers come from the `ShortRun` job (3 warmups, 3 iterations, 1 launch) — indicative rather than publication-quality. Timings will vary on other hardware; concentrate on the **Allocated** column, which is deterministic.

## Host info

```
BenchmarkDotNet v0.13.12, Windows 11 (10.0.26200.8246)
Unknown processor
.NET SDK 9.0.313
  [Host]   : .NET 9.0.15 (9.0.1526.17522), X64 RyuJIT AVX2
  ShortRun : .NET 9.0.15 (9.0.1526.17522), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3
```

## Results

| Method              |         Mean |        Error |      StdDev |   Gen0 | Allocated |
| ------------------- | -----------: | -----------: | ----------: | -----: | --------: |
| Ulid_New            |   116.721 ns |   283.159 ns |  15.5209 ns |      - |         - |
| Ulid_ToString       |    54.302 ns |   205.869 ns |  11.2844 ns | 0.0063 |      80 B |
| Ulid_Parse          |   118.381 ns |    74.489 ns |   4.0830 ns |      - |         - |
| Ulid_TryParseSpan   |   132.873 ns |    31.117 ns |   1.7056 ns |      - |         - |
| Uuid7_New           |   188.486 ns |   147.752 ns |   8.0988 ns |      - |         - |
| Uuid7_ToString      |    23.861 ns |    26.893 ns |   1.4741 ns | 0.0076 |      96 B |
| Uuid7_Parse         |    55.069 ns |    45.007 ns |   2.4670 ns |      - |         - |
| Snowflake_New       | 3,110.505 ns | 2,549.093 ns | 139.7244 ns |      - |         - |
| Snowflake_ToString  |    30.363 ns |    55.049 ns |   3.0174 ns | 0.0051 |      64 B |
| Snowflake_Parse     |    40.529 ns |   160.535 ns |   8.7994 ns |      - |         - |
| Sequential_New      |     6.709 ns |     1.999 ns |   0.1096 ns |      - |         - |
| Sequential_ToString |     2.174 ns |    12.287 ns |   0.6735 ns |      - |         - |

Legend:

- **Mean / Error / StdDev** — timing statistics (1 ns = 10⁻⁹ s).
- **Gen0** — Gen 0 collects per 1,000 operations.
- **Allocated** — managed bytes allocated per single call (`-` means the benchmark did not allocate).

## Observations

**Zero-allocation paths.** Every `New()`, every `Parse`, and every `TryParse(ReadOnlySpan<char>, …)` reports `-` in the Allocated column. That holds for all four strategies:

- `Ulid_New`, `Uuid7_New`, `Snowflake_New`, `Sequential_New` — 0 B.
- `Ulid_Parse`, `Uuid7_Parse`, `Snowflake_Parse` — 0 B.
- `Ulid_TryParseSpan` — 0 B (the span overload round-trips without touching the heap).

This is the zero-allocation claim from the README, observed in the wild: generation and parsing of a `[TypedId]` never produces GC pressure.

**`ToString` necessarily allocates.** Any method that returns a `string` must allocate that string — there is no way to produce a 0-byte managed string. The three `*_ToString` rows show exactly the minimum allocation the string object requires for its payload:

- `Ulid_ToString` — 80 B (26-char Crockford base32).
- `Uuid7_ToString` — 96 B (36-char canonical form with dashes).
- `Snowflake_ToString` — 64 B (19-character decimal max).

`Sequential_ToString` reports `-` because the sample value sits in the small-integer cache that `long.ToString` returns by reference — single- and double-digit sequence values do not allocate. Once the counter grows past that cache boundary the call allocates a fresh string, identical in shape to the Snowflake row.

If you need a zero-allocation textual representation, use the span-producing `TryFormat` / `TryWriteStringify` path exposed by the generator instead of `ToString`.

**Timing notes.** `Snowflake_New` is substantially slower (~3 µs) because the benchmark fixture spins in the Snowflake sequence loop waiting for the next millisecond tick; this is expected behaviour under sustained load and not representative of steady-state throughput at realistic call rates. `Sequential_New` at ~7 ns is dominated by a single interlocked increment. The short-run job produces wide confidence intervals — for precise numbers re-run without `--job short`.

**Validation.** The numbers confirm the README's zero-allocation guarantee for the hot paths (`New`, `Parse`, span-based `TryParse`). The only allocations in the entire suite come from `ToString`, which is an unavoidable property of returning a `string` — applications that render IDs to strings on the hot path should reach for the span-writing overloads.
