# ZeroAlloc.ValueObjects — `[TypedId]` extension design

**Goal:** Add a `[TypedId]` source generator attribute alongside the existing `[ValueObject]` so consumers can declare strongly-typed identifier structs (ULID, UUID7, Snowflake, Sequential) with zero hand-written code and zero runtime reflection.

**Scope:** v1 ships all four strategies, both backing types, JSON + Minimal API binding in the core package, and EF Core support in a new adapter package.

---

## Architecture

Ship inside the existing `ZeroAlloc.ValueObjects` repo. Two existing projects get additions plus one new project for EF Core.

| Project | Purpose |
|---|---|
| `ZeroAlloc.ValueObjects` (existing) | `TypedIdAttribute`, `TypedIdDefaultAttribute`, `IdStrategy` enum, `BackingType` enum, `ISnowflakeWorkerIdProvider`, `TypedIdException`, static runtime helpers (`UlidCore`, `Uuid7Core`, `SnowflakeCore`, `SequentialCore`, `TypedIdRuntime`), DI extensions (`AddSnowflakeWorkerId`). |
| `ZeroAlloc.ValueObjects.Generator` (existing) | New sibling `TypedIdGenerator : IIncrementalGenerator`, separate parser and writer from `ValueObjectGenerator`. Shares utility code (Roslyn helpers, `IsExternalInit` polyfill). |
| `ZeroAlloc.ValueObjects.EfCore` (new) | EF Core adapter: `TypedIdValueConverter<TId, TBacking>`, `ModelConfigurationBuilder.AddTypedIdConventions()` extension. Depends on `Microsoft.EntityFrameworkCore`. |

No new package for JSON or Minimal API — those use BCL contracts (`JsonConverter<T>`, `IParsable<T>`, `ISpanParsable<T>`) that have no external dependencies.

---

## Consumer API

```csharp
// Per-type declaration
[TypedId(Strategy = IdStrategy.Ulid)]
public readonly partial record struct OrderId;

[TypedId(Strategy = IdStrategy.Snowflake)]
public readonly partial record struct MessageId;

[TypedId(Strategy = IdStrategy.Sequential, Backing = BackingType.Int64)]
public readonly partial record struct TestStableId;

// Optional assembly-level default (if omitted, built-in default = Ulid + Guid backing)
[assembly: TypedIdDefault(Strategy = IdStrategy.Ulid, Backing = BackingType.Guid)]

// Now the struct declaration can omit Strategy:
[TypedId]
public readonly partial record struct ProductId;

// DI (only if Snowflake or EF Core is used)
services.AddSnowflakeWorkerId(workerId: 5);                     // literal
services.AddSnowflakeWorkerId(envVar: "POD_ORDINAL");           // env var
services.AddSnowflakeWorkerId(sp => sp.GetRequiredService<IMachineIdProvider>().Id);  // DI

// EF Core convention (in DbContext.ConfigureConventions)
builder.AddTypedIdConventions();

// Minimal API binding — no setup needed
app.MapGet("/orders/{id}", (OrderId id) => …);
```

---

## Attribute shape and defaults

```csharp
[AttributeUsage(AttributeTargets.Struct, Inherited = false)]
public sealed class TypedIdAttribute : Attribute
{
    public IdStrategy Strategy { get; init; }
    public BackingType Backing { get; init; }       // Default = 0 → auto-pick
}

[AttributeUsage(AttributeTargets.Assembly, Inherited = false)]
public sealed class TypedIdDefaultAttribute : Attribute
{
    public IdStrategy Strategy { get; init; } = IdStrategy.Ulid;
    public BackingType Backing { get; init; }       // Default = 0 → auto-pick
}

public enum IdStrategy { Ulid, Uuid7, Snowflake, Sequential }
public enum BackingType { Default = 0, Guid, Int64 }
```

**Resolution order** for any `[TypedId]` struct:

1. If per-struct `Strategy` is set → use it; else read `[assembly: TypedIdDefault].Strategy`; else default `Ulid`.
2. Same order for `Backing`. `Default` → auto-pick: `Ulid/Uuid7 → Guid`, `Snowflake/Sequential → Int64`.
3. Incompatible combinations (`Snowflake + Guid`, `Ulid + Int64`) produce **ZATI001** and the generator refuses to emit.

**Declaration requirements** — any violation is a build error:

- Must be `readonly partial record struct` (ZATI002).
- Must have an empty body; the generator owns the `Value` field (ZATI003).
- `[TypedIdDefault]` must target the assembly (reserved diagnostic ZATI004 — enforced by `[AttributeUsage(AttributeTargets.Assembly)]` on the attribute itself, not a generator diagnostic).

---

## Generated code shape

One `{StructName}.TypedId.g.cs` per struct, emitted into a conventional namespace-nested folder.

### Guid-backed (ULID / UUID7)

```csharp
public readonly partial record struct OrderId :
    IEquatable<OrderId>,
    IComparable<OrderId>,
    IParsable<OrderId>,
    ISpanParsable<OrderId>
{
    public Guid Value { get; }

    public OrderId(Guid value) => Value = value;

    public static OrderId New() => new(UlidCore.NewGuid());

    public override string ToString() => UlidCore.ToBase32(Value);

    public static OrderId Parse(string s, IFormatProvider? provider = null) =>
        TryParse(s.AsSpan(), provider, out var id) ? id
        : throw new FormatException($"Value '{s}' is not a valid OrderId.");

    public static bool TryParse(string? s, IFormatProvider? provider, out OrderId result) { … }
    public static OrderId Parse(ReadOnlySpan<char> s, IFormatProvider? provider) { … }
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out OrderId result) { … }

    public int CompareTo(OrderId other) => Value.CompareTo(other.Value);

    [JsonConverter(typeof(JsonConv))]
    private sealed class JsonConv : JsonConverter<OrderId> { /* string reader/writer */ }
}
```

### Int64-backed (Snowflake / Sequential)

Same shape, with `public long Value { get; }`, decimal string form, and strategy-specific `New()`:

- Snowflake: `public static MessageId New() => new(SnowflakeCore.Next());` — `SnowflakeCore` reads `TypedIdRuntime.SnowflakeProvider` on first call.
- Sequential: `public static TestStableId New() => new(SequentialCore.Next());` — `SequentialCore` is an `Interlocked.Increment` on a static counter.

### Runtime helpers (BCL-only, no dependencies)

- `UlidCore` — stackalloc-based ULID generation + Crockford base32 encode/decode. Zero-alloc path.
- `Uuid7Core` — RFC 9562 UUIDv7 construction (48-bit unix ms + version/variant bits + randomness).
- `SnowflakeCore` — 41-bit timestamp + 10-bit worker + 12-bit sequence = 63 bits, packed into `long`. Uses `Interlocked.CompareExchange` on a packed state.
- `SequentialCore` — `private static long _counter; Interlocked.Increment(ref _counter)`.
- `TypedIdRuntime` — static holder for `ISnowflakeWorkerIdProvider?`, populated by `AddSnowflakeWorkerId` via a startup `IHostedService`.

### Equality, hash, operators

Not generated — `record struct` gives us structural equality and hash for free from the single `Value` field. `[TypedId]` does not compose with `[ValueObject]`; if a type has multiple fields, it's not a typed id.

---

## Integration points

### JSON (System.Text.Json, core package)

Each generated struct carries `[JsonConverter(typeof(StructName.JsonConv))]`. The nested `JsonConv : JsonConverter<T>` reads a string and calls `Parse`, writes `ToString()`. Fully AOT-safe — the converter type is known at compile time, no reflection, no runtime registration needed.

### Minimal API / MVC binding (core package)

The generated `IParsable<T>` + `ISpanParsable<T>` implementations are automatically consumed by ASP.NET Core's model binder. `app.MapGet("/orders/{id}", (OrderId id) => ...)` binds the route token by calling `OrderId.TryParse`. No setup.

### Snowflake worker ID (core package)

```csharp
public static class SnowflakeServiceCollectionExtensions
{
    public static IServiceCollection AddSnowflakeWorkerId(this IServiceCollection services, int workerId);
    public static IServiceCollection AddSnowflakeWorkerId(this IServiceCollection services, string envVar, int fallback = 0);
    public static IServiceCollection AddSnowflakeWorkerId(this IServiceCollection services, Func<IServiceProvider, int> factory);
}
```

Each overload registers an `IHostedService` that reads the worker ID on `StartAsync` and sets `TypedIdRuntime.SnowflakeProvider`. First `Snowflake.New()` call after host start sees the provider; a call before the host starts throws `TypedIdException` with a clear message.

Resolution order on first `New()`:
1. `TypedIdRuntime.SnowflakeProvider` if set → use it.
2. Env var `ZA_SNOWFLAKE_WORKER_ID` if present → parse and use.
3. Throw `TypedIdException`.

### EF Core (`ZeroAlloc.ValueObjects.EfCore` package)

```csharp
public static class TypedIdModelConfigurationExtensions
{
    public static ModelConfigurationBuilder AddTypedIdConventions(this ModelConfigurationBuilder builder, Assembly? scan = null);
}

public sealed class TypedIdValueConverter<TId, TBacking> : ValueConverter<TId, TBacking>
    where TId : struct, IParsable<TId>   // constrained to TypedId-generated structs
{
    // Uses reflection once at startup to read the `Value` property.
    // After that, the compiled converter expression is cached by EF Core.
}
```

`AddTypedIdConventions` scans the given assembly (defaulting to the DbContext's assembly) for structs marked `[TypedId]` and registers a `TypedIdValueConverter<TId, Guid>` or `TypedIdValueConverter<TId, long>` per type based on the backing. Per-property opt-out via `HasConversion` stays available.

**Guid-backed IDs** default to a `uniqueidentifier` / `uuid` column; **Int64-backed IDs** default to `bigint`. Users who want ULIDs stored as `char(26)` can opt into a string converter via `HasConversion<TypedIdStringConverter<OrderId>>()`.

---

## Error handling

### Compile-time diagnostics

| ID | Severity | Condition | Message |
|---|---|---|---|
| `ZATI001` | Error | Strategy + backing incompatible | "{Strategy} requires {RequiredBacking}; {ProvidedBacking} is incompatible." |
| `ZATI002` | Error | Target is not `readonly partial record struct` | "[TypedId] requires `readonly partial record struct`; {T} is {actualModifiers}." |
| `ZATI003` | Error | Struct body declares fields or properties | "[TypedId] structs must have an empty body; generator emits the `Value` field." |
| `ZATI004` | — | Reserved — enforced by `[AttributeUsage(AttributeTargets.Assembly)]` on `TypedIdDefaultAttribute`; no generator diagnostic emitted. | — |
| `ZATI005` | Warning | Struct declared as partial in multiple files | Advisory — may drift from generator output. |

### Runtime exceptions

- `FormatException` from `Parse` on input that doesn't match the strategy format. Standard BCL behaviour.
- `OverflowException` from `Snowflake.New()` on clock rollback or per-millisecond sequence overflow. Caller should retry on next tick.
- `TypedIdException : InvalidOperationException` thrown on first `Snowflake.New()` when no worker ID is configured. Message names `AddSnowflakeWorkerId` and `ZA_SNOWFLAKE_WORKER_ID` for remediation.

### No silent fallbacks

- Snowflake never picks a random worker ID. Duplicate IDs across nodes are worse than a startup crash.
- Sequential counter is not persisted across process restarts; this is by design and documented in XML remarks. Sequential is for test stability, not production uniqueness.

---

## Testing

### Generator snapshot tests (`ZeroAlloc.ValueObjects.Generator.Tests`)

- One Verify snapshot per (strategy × backing × scenario): 4 strategies × 2 backings valid combinations + default-resolution + assembly-default + multi-struct-in-one-file.
- Uses existing Verify + Roslyn setup.
- Catches accidental drift in emitted code.

### Diagnostic tests

- One test per ZATI diagnostic asserting it fires at the expected location with the expected message template.
- Negative: valid attributes produce zero diagnostics.

### Runtime behaviour tests (`ZeroAlloc.ValueObjects.Tests`)

- ULID: monotonicity (1000 `New()` in ≤1 ms still strictly increasing); round-trip `ToString`→`Parse`; `TryParse` rejects invalid base32.
- UUID7: time-ordered property; RFC 9562 version/variant bits set correctly.
- Snowflake: missing-config error path; sequence exhaustion behaviour; clock rollback → `OverflowException`.
- Sequential: 16 concurrent tasks each calling `New()` 10k times yield strictly distinct values.
- JSON round-trip per strategy — serialise, deserialise, assert equal.
- Minimal API binding smoke test per backing (via `WebApplicationFactory` and a trivial route).

### Allocation tests (`ZeroAlloc.ValueObjects.Benchmarks`)

- BenchmarkDotNet bench per strategy's `New()` + `ToString()` + `Parse()` with `[MemoryDiagnoser]`.
- CI asserts 0 allocated bytes for `New()`, `TryParse(ReadOnlySpan<char>)` across all strategies; `ToString()` allocates only the final string.

### EF Core tests (`ZeroAlloc.ValueObjects.EfCore.Tests`, new project)

- SQLite in-memory `DbContext` with entities keyed by each backing type.
- Round-trip insert → query → assert equal.
- Convention test: `builder.AddTypedIdConventions()` alone produces correct SQL column types.
- Opt-out test: per-property `HasConversion` overrides convention.

---

## Out of scope for v1

- **v2 knobs** (deferred): `[TypedId(Format = IdFormat.Base62)]`, custom prefix (`order_01HK...`), obfuscation/shuffling, distributed Sequential (needs a counter coordinator — either Snowflake or a DB sequence is the answer).
- **AspNetCore TypeConverter** fallback for classic MVC binding — `IParsable<T>` covers Minimal APIs; MVC users can opt in with a one-liner if needed. Not worth the ceremony for v1.
- **Composition with `[ValueObject]`** — not needed. A TypedId is a single-field record struct; record-struct equality already matches what `[ValueObject]` generates.
