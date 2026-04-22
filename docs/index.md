---
id: index
title: ZeroAlloc.ValueObjects
slug: /
description: Zero-allocation source-generated value object equality for .NET — no boxing, no iterator allocations.
sidebar_position: 1
---

# ZeroAlloc.ValueObjects — Documentation

Zero-allocation, source-generated value object equality for existing .NET domain types.

Same performance as `record` — without forcing the `record` keyword on your domain model.

---

## Contents

| Page | Description |
|---|---|
| [Why this library?](why.md) | The problem with CFE, why not just use `record` |
| [Installation](installation.md) | NuGet install, requirements |
| [Getting Started](getting-started.md) | Step-by-step quickstart with core concepts |
| [Attribute Reference](attributes.md) | `[ValueObject]`, `[EqualityMember]`, `[IgnoreEqualityMember]` |
| [Member Selection](member-selection.md) | How properties are chosen for equality |
| [What Gets Generated](generated-output.md) | Exact code the generator emits |
| [Struct vs. Class](struct-vs-class.md) | When to use each, `ForceClass` |
| [Nullable Properties](nullable-properties.md) | Null-safe comparison generation |
| [Usage Patterns](patterns.md) | Dictionary keys, HashSets, LINQ, EF Core, pattern matching |
| [Migration Guide](migration.md) | From CFE `ValueObject`, from manual equality |
| [Performance](performance.md) | Benchmark results and how to run them |
| [Design & Limitations](design.md) | Trade-offs, things not generated |
| [Troubleshooting](troubleshooting.md) | Common errors and fixes |
| [Testing](testing.md) | Testing value objects — equality, hash codes, serialization |
| **Examples** | |
| [E-Commerce](examples/ecommerce.md) | `ProductId`, `Money`, `ShippingAddress`, `Discount` |
| [Finance](examples/finance.md) | `Iban`, `CurrencyPair`, `AccountNumber` |
| [HR / Identity](examples/hr-identity.md) | `EmailAddress`, `EmployeeId`, `FullName` |
| [Geospatial](examples/geospatial.md) | `Coordinates`, `GeoRegion` |
| [Scheduling](examples/scheduling.md) | `DateRange`, `TimeSlot` |

---

## Typed Identifiers

`[TypedId]` is the companion attribute for strongly-typed IDs — `OrderId`, `UserId`, `MessageId`. Built-in ULID / UUIDv7 / Snowflake / Sequential strategies, JSON, minimal API, and EF Core support out of the box.

| Page | Description |
|---|---|
| [Typed Identifiers overview](typed-id/index.md) | When to use `[TypedId]` vs `[ValueObject]`, section contents |
| [Getting started](typed-id/getting-started.md) | Install, declare, use — five-minute introduction |
| [Strategies](typed-id/strategies.md) | ULID, UUIDv7, Snowflake, Sequential — formats and trade-offs |
| [Snowflake configuration](typed-id/snowflake-config.md) | Worker-ID setup via DI, env var, or factory |
| [JSON serialization](typed-id/json.md) | `System.Text.Json` integration, AOT safety |
| [ASP.NET minimal API binding](typed-id/aspnet.md) | Route and query binding via `IParsable<T>` |
| [EF Core integration](typed-id/efcore.md) | `ZeroAlloc.ValueObjects.EfCore` conventions |
| [Diagnostics](typed-id/diagnostics.md) | `ZATI001`–`ZATI005` reference |
| [Production checklist](typed-id/production.md) | Operational caveats and audit questions |
| [Internals](typed-id/internals.md) | Generator pipeline and zero-allocation techniques |

---

## Quick Example

```csharp
using ZeroAlloc.ValueObjects;

[ValueObject]
public partial class Money
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }
}
```

```csharp
var a = new Money(100m, "USD");
var b = new Money(100m, "USD");

a == b                  // true
a.GetHashCode()         // same as b — safe as dict key
a.ToString()            // "Money { Amount = 100, Currency = USD }"
```

Zero allocations. No `record` keyword. No base class required.
