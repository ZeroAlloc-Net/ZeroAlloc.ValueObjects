# ZeroAlloc.ValueObjects

[![NuGet](https://img.shields.io/nuget/v/ZeroAlloc.ValueObjects.svg)](https://www.nuget.org/packages/ZeroAlloc.ValueObjects)
[![Build](https://github.com/ZeroAlloc-Net/ZeroAlloc.ValueObjects/actions/workflows/ci.yml/badge.svg)](https://github.com/ZeroAlloc-Net/ZeroAlloc.ValueObjects/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![AOT](https://img.shields.io/badge/AOT--Compatible-passing-brightgreen)](https://learn.microsoft.com/dotnet/core/deploying/native-aot/)
[![GitHub Sponsors](https://img.shields.io/github/sponsors/MarcelRoozekrans?style=flat&logo=githubsponsors&color=ea4aaa&label=Sponsor)](https://github.com/sponsors/MarcelRoozekrans)

Zero-allocation source-generated ValueObject equality for your existing domain types.

Same performance as `record` — without forcing the `record` keyword on your domain model. Add `[ValueObject]` to any `partial class` or `partial struct` and the generator emits `Equals`, `GetHashCode`, `==`, `!=`, and `ToString` with no heap allocations.

## Install

```bash
dotnet add package ZeroAlloc.ValueObjects
```

## Quick start

```csharp
// Annotate any existing partial class — no keyword changes, no base class
[ValueObject]
public partial class Money
{
    public decimal Amount { get; }
    public string Currency { get; }
    public Money(decimal amount, string currency) => (Amount, Currency) = (amount, currency);
}

// Use standard equality — zero allocations
var a = new Money(10m, "USD");
var b = new Money(10m, "USD");

bool equal = a == b;            // true
bool same  = a.Equals(b);       // true  — IEquatable<Money> fast path
int  hash  = a.GetHashCode();   // same as b.GetHashCode() — safe as dict key
string s   = a.ToString();      // "Money { Amount = 10, Currency = USD }"
```

## Performance

`ZeroAlloc.ValueObjects` matches `record` and `record struct` performance exactly. The only allocating variant is `CSharpFunctionalExtensions.ValueObject`.

| Method                          | Mean    | Allocated |
|---------------------------------|--------:|----------:|
| CFE_Equals                      | 45.2 ns | 96 B      |
| Record_Equals                   |  3.1 ns | 0 B       |
| RecordStruct_Equals             |  2.8 ns | 0 B       |
| **ZeroAlloc_Equals**            |  3.1 ns | **0 B**   |
| **ZeroAllocStruct_Equals**      |  2.8 ns | **0 B**   |
| CFE_GetHashCode                 | 38.7 ns | 88 B      |
| Record_GetHashCode              |  2.4 ns | 0 B       |
| RecordStruct_GetHashCode        |  2.2 ns | 0 B       |
| **ZeroAlloc_GetHashCode**       |  2.4 ns | **0 B**   |
| **ZeroAllocStruct_GetHashCode** |  2.2 ns | **0 B**   |

Full methodology and more scenarios: [docs/performance.md](docs/performance.md)

## Features

- Zero allocations — no iterator state machine, no boxing
- Works on existing `partial class` and `partial struct` — no refactoring required
- Can inherit from non-record base classes
- Fine-grained member control with `[EqualityMember]` (opt-in) and `[IgnoreEqualityMember]` (opt-out)
- No extra generated members — no `with`, no `Deconstruct`, no `EqualityContract`
- Null-safe comparison for nullable reference type properties
- `HashCode.Combine` for ≤8 properties, incremental `HashCode.Add` for 9+

## Why not just use `record`?

| | `record` | `ZeroAlloc.ValueObjects` |
|---|---|---|
| Zero allocation | ✓ | ✓ |
| Works on existing `class`/`struct` | ✗ — forces `record` keyword | ✓ |
| Can inherit from non-record base | ✗ | ✓ |
| Fine-grained member control | ✗ | `[EqualityMember]` / `[IgnoreEqualityMember]` |
| No extra generated members | ✗ — adds `EqualityContract`, `with`, deconstruct | ✓ |
| Struct support | `record struct` | `partial struct` |

## Typed identifiers

`[TypedId]` is the companion attribute for strongly-typed IDs — `OrderId`, `UserId`, `MessageId`. It solves the same problem as the ValueObject attribute, but tailored for single-value identifiers with built-in generation strategies.

### Basic usage

```csharp
using ZeroAlloc.ValueObjects;

[TypedId(Strategy = IdStrategy.Ulid)]
public readonly partial record struct OrderId;

// Usage
OrderId id = OrderId.New();          // monotonic ULID
string s = id.ToString();            // "01ARZ3NDEKTSV4RRFFQ69G5FAV" — 26-char base32
OrderId parsed = OrderId.Parse(s);   // round-trip
```

### Strategies

| Strategy | Backing | Format | Use case |
|---|---|---|---|
| `Ulid` (default) | `Guid` | 26-char Crockford base32 | General-purpose, sortable, URL-safe |
| `Uuid7` | `Guid` | 36-char hyphenated UUID | Time-ordered with standard UUID interop |
| `Snowflake` | `long` | Decimal string | Distributed systems needing 64-bit IDs |
| `Sequential` | `long` | Decimal string | Test stability only — not for production |

### Assembly-level default

```csharp
[assembly: TypedIdDefault(Strategy = IdStrategy.Ulid)]

[TypedId]                                       // resolves to Ulid from the assembly default
public readonly partial record struct ProductId;

[TypedId(Strategy = IdStrategy.Snowflake)]      // per-struct override
public readonly partial record struct MessageId;
```

### Snowflake worker ID configuration

Snowflake IDs encode a 10-bit worker ID so multiple processes can mint IDs concurrently without collision. Configure at startup:

```csharp
builder.Services.AddSnowflakeWorkerId(workerId: 5);
builder.Services.AddSnowflakeWorkerId(envVar: "POD_ORDINAL", fallback: 0);
builder.Services.AddSnowflakeWorkerId(sp => sp.GetRequiredService<IMachineIdProvider>().Id);
```

If no provider is registered, `Snowflake.New()` falls back to `ZA_SNOWFLAKE_WORKER_ID` env var, then throws `TypedIdException`.

### EF Core

Install `ZeroAlloc.ValueObjects.EfCore` and register the convention:

```csharp
protected override void ConfigureConventions(ModelConfigurationBuilder builder)
{
    builder.AddTypedIdConventions();
}
```

All `[TypedId]` structs in the DbContext's assembly are auto-mapped: Guid-backed → `uniqueidentifier`/`uuid`, long-backed → `bigint`. Per-property `HasConversion` still overrides.

### Minimal API binding

No setup needed — the generator emits `IParsable<T>` + `ISpanParsable<T>`, so `app.MapGet("/orders/{id}", (OrderId id) => …)` just works.

### JSON

Each TypedId carries `[JsonConverter]` pointing at a nested converter that reads/writes a string. Fully AOT-safe. No `JsonSerializerContext` wiring required for basic use; if you're source-generating `JsonSerializerContext`, include the TypedId types there too.

### Compile-time diagnostics

| ID | Severity | Meaning |
|---|---|---|
| `ZATI001` | Error | Incompatible strategy/backing (e.g. Snowflake + Guid) |
| `ZATI002` | Error | Type is not `readonly partial record struct` |
| `ZATI003` | Error | Struct body declares fields — generator owns `Value` |
| `ZATI005` | Warning | Struct declared partial across multiple files |

### Production checklist

- **Sequential is not for production**. The counter resets on process restart. Use it only in tests where deterministic IDs matter.
- **Snowflake worker IDs must be unique across all producing processes**. `AddSnowflakeWorkerId` cannot detect duplicates. Coordinate via orchestrator ordinals (Kubernetes pod index, Nomad alloc index) or a central registry. Duplicate worker IDs silently produce colliding IDs.
- **Clock skew matters**. Snowflake generation handles small rollbacks by pinning to the last observed millisecond, but severe skew (>5s) throws `TypedIdException`. Run NTP-synced or accept the brief unavailability.
- **Process restart loses Sequential state but not Snowflake or ULID/UUID7 ordering**. ULID/UUID7 are globally safe to restart; Snowflake is safe if worker ID is stable across restarts.

## Documentation

| Page | Description |
|------|-------------|
| [Why this library?](docs/why.md) | The problem with CFE, why not just use `record` |
| [Installation](docs/installation.md) | NuGet install, .NET version requirements |
| [Getting Started](docs/getting-started.md) | Step-by-step quickstart with core concepts |
| [Attribute Reference](docs/attributes.md) | `[ValueObject]`, `[EqualityMember]`, `[IgnoreEqualityMember]` |
| [Member Selection](docs/member-selection.md) | How properties are chosen for equality |
| [Generated Output](docs/generated-output.md) | Exact code the generator emits |
| [Struct vs. Class](docs/struct-vs-class.md) | When to use each, `ForceClass` |
| [Nullable Properties](docs/nullable-properties.md) | Null-safe comparison generation |
| [Usage Patterns](docs/patterns.md) | Dictionary keys, HashSets, LINQ, EF Core, pattern matching |
| [Migration Guide](docs/migration.md) | From CFE `ValueObject`, from manual equality |
| [Performance](docs/performance.md) | Benchmark results and how to run them |
| [Design Decisions](docs/design.md) | Trade-offs, intentional omissions |
| [Troubleshooting](docs/troubleshooting.md) | Common errors and fixes |
| [Testing](docs/testing.md) | Writing unit tests for value object equality |
| **Examples** | |
| [E-Commerce](docs/examples/ecommerce.md) | `ProductId`, `Money`, `ShippingAddress`, `Discount` |
| [Finance](docs/examples/finance.md) | `Iban`, `CurrencyPair`, `AccountNumber` |
| [HR / Identity](docs/examples/hr-identity.md) | `EmailAddress`, `EmployeeId`, `FullName` |
| [Geospatial](docs/examples/geospatial.md) | `Coordinates`, `GeoRegion` |
| [Scheduling](docs/examples/scheduling.md) | `DateRange`, `TimeSlot` |

## License

MIT
