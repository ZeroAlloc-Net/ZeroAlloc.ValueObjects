---
id: typed-id-index
title: Typed Identifiers
slug: /docs/typed-id
description: Overview of [TypedId] — strongly-typed identifier structs with ULID, UUIDv7, Snowflake, and Sequential strategies.
sidebar_position: 20
---

# Typed Identifiers

`[TypedId]` is the companion attribute for strongly-typed identifiers — `OrderId`, `UserId`, `MessageId`, etc. It solves the same kind of problem as `[ValueObject]` but tailored for single-value IDs with built-in generation strategies, JSON support, EF Core conversion, and ASP.NET minimal-API binding.

| When to use | Attribute |
|---|---|
| A single value that *is* an identifier (has `New()`, `Parse()`, serializers) | `[TypedId]` |
| A domain concept with multiple properties (structural equality across them) | `[ValueObject]` |

A `[TypedId]` struct is a `readonly partial record struct` with one generator-owned `Value` field. The generator emits `New()`, `Parse`/`TryParse`, `ToString`, `CompareTo`, a nested `JsonConverter`, and `IParsable<T>` / `ISpanParsable<T>` implementations. Structural equality and `GetHashCode` come for free from `record struct`.

## In this section

| Page | Topic |
|---|---|
| [Getting started](getting-started.md) | Five-minute introduction — install, annotate, use |
| [Strategies](strategies.md) | ULID, UUIDv7, Snowflake, Sequential — what they are and when to use each |
| [Snowflake configuration](snowflake-config.md) | Worker ID setup (DI, env var, factory) |
| [JSON serialization](json.md) | System.Text.Json integration; AOT behaviour |
| [ASP.NET minimal API binding](aspnet.md) | Route and query binding via `IParsable<T>` |
| [EF Core integration](efcore.md) | `ZeroAlloc.ValueObjects.EfCore` adapter and conventions |
| [Diagnostics](diagnostics.md) | ZATI001–ZATI005 reference |
| [Production checklist](production.md) | Caveats and operational notes |
| [Internals](internals.md) | How the generator works; zero-allocation proof |

## Minimal example

```csharp
using ZeroAlloc.ValueObjects;

[TypedId(Strategy = IdStrategy.Ulid)]
public readonly partial record struct OrderId;

// Usage
OrderId id = OrderId.New();          // monotonic ULID
string s = id.ToString();            // "01ARZ3NDEKTSV4RRFFQ69G5FAV"
OrderId parsed = OrderId.Parse(s);   // round-trips
bool equal = id == parsed;           // true
```
