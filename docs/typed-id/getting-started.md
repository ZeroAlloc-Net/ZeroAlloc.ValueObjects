---
id: typed-id-getting-started
title: TypedId Getting Started
slug: /docs/typed-id/getting-started
description: Install, declare a TypedId struct, and use it across JSON, routing, and EF Core in five minutes.
sidebar_position: 21
---

# Getting Started

A TypedId replaces a bare `Guid`/`long`/`string` identifier with a dedicated struct. You get compile-time protection against mixing identifier types, built-in generation, parsing, serialization, and database mapping — all with zero hand-written code.

## Step 1 — Install

The `[TypedId]` attribute ships in the core package:

```bash
dotnet add package ZeroAlloc.ValueObjects
```

For database integration, add the adapter (see [EF Core integration](efcore.md)):

```bash
dotnet add package ZeroAlloc.ValueObjects.EfCore
```

## Step 2 — Declare the struct

Mark a `readonly partial record struct` with `[TypedId]` and pick a strategy. The body must be empty — the generator owns the `Value` field.

```csharp
using ZeroAlloc.ValueObjects;

[TypedId(Strategy = IdStrategy.Ulid)]
public readonly partial record struct OrderId;
```

## Step 3 — Use it

```csharp
OrderId a = OrderId.New();              // monotonic ULID
OrderId b = OrderId.New();

Console.WriteLine(a);                   // "01ARZ3NDEKTSV4RRFFQ69G5FAV"
Console.WriteLine(a == b);              // false — every New() is unique
Console.WriteLine(a < b);               // true — ULIDs are lexicographically sortable

OrderId parsed = OrderId.Parse(a.ToString());
Console.WriteLine(a == parsed);         // true — round-trip
```

`TryParse` follows the standard BCL shape:

```csharp
if (OrderId.TryParse(userInput, out var id))
{
    // handle id
}
```

## Step 4 — Set an assembly-wide default

If most of your IDs use the same strategy, set it once:

```csharp
// AssemblyInfo.cs (or any top-level file)
[assembly: TypedIdDefault(Strategy = IdStrategy.Ulid)]
```

Then omit the strategy on individual structs:

```csharp
[TypedId]
public readonly partial record struct ProductId;   // resolves to Ulid from the default

[TypedId(Strategy = IdStrategy.Snowflake)]
public readonly partial record struct MessageId;   // per-struct override
```

See [Strategies](strategies.md) for the full resolution order.

## Step 5 — JSON

No registration needed. The generator emits a `[JsonConverter]` attribute on the struct pointing at a nested converter that reads/writes the string form.

```csharp
record OrderCreated(OrderId Id, decimal Total);

string json = JsonSerializer.Serialize(new OrderCreated(OrderId.New(), 42m));
// {"Id":"01ARZ3NDEKTSV4RRFFQ69G5FAV","Total":42}

var roundTripped = JsonSerializer.Deserialize<OrderCreated>(json)!;
```

Details in [JSON serialization](json.md).

## Step 6 — ASP.NET minimal API

The generated `IParsable<T>` implementation is consumed automatically by the model binder:

```csharp
app.MapGet("/orders/{id}", (OrderId id) => $"Looking up order {id}");

// GET /orders/01ARZ3NDEKTSV4RRFFQ69G5FAV → "Looking up order 01ARZ3..."
// GET /orders/not-a-ulid                   → 400 Bad Request
```

Details in [ASP.NET minimal API binding](aspnet.md).

## Step 7 — EF Core

Install `ZeroAlloc.ValueObjects.EfCore` and register the convention once:

```csharp
public sealed class AppDbContext : DbContext
{
    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        builder.AddTypedIdConventions();
    }
}
```

Every `[TypedId]` struct defined in the DbContext's assembly maps automatically: Guid-backed IDs become `uniqueidentifier`/`uuid` columns, Int64-backed IDs become `bigint`. Details in [EF Core integration](efcore.md).

## What next

- Pick a strategy: [Strategies](strategies.md)
- Ship Snowflake to production: [Snowflake configuration](snowflake-config.md)
- Understand the guardrails: [Diagnostics](diagnostics.md), [Production checklist](production.md)
