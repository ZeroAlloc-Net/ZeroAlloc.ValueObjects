---
id: typed-id-json
title: TypedId JSON Serialization
slug: /docs/typed-id/json
description: System.Text.Json integration via auto-registered JsonConverter, AOT safety, and envelope usage.
sidebar_position: 24
---

# JSON Serialization

Every `[TypedId]` struct is serializable through `System.Text.Json` in the default reflection-based mode out of the box. The generator emits a nested `JsonConverter<T>` and attaches a `[JsonConverter]` attribute to the struct, so the default `JsonSerializer.Serialize`/`Deserialize` discovers it automatically — no registration, no `JsonSerializerOptions` wiring.

When you use the source-generated `JsonSerializerContext` pipeline, an extra one-line registration is required. See [Source-gen contexts](#source-gen-contexts-jsonserializercontext) below.

## Generated shape

For a ULID-backed `OrderId` the generator emits roughly:

```csharp
[JsonConverter(typeof(OrderId.TypedIdJsonConverter))]
public readonly partial record struct OrderId
{
    public Guid Value { get; }
    public OrderId(Guid value) => Value = value;
    // ... New, Parse, TryParse, ToString, CompareTo ...

    public sealed class TypedIdJsonConverter : JsonConverter<OrderId>
    {
        public override OrderId Read(
            ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var s = reader.GetString();
            return OrderId.Parse(s!, provider: null);
        }

        public override void Write(
            Utf8JsonWriter writer, OrderId value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}
```

The converter is emitted as `public sealed` so consumers in other assemblies can instantiate it for the explicit-registration scenario described below. The name `TypedIdJsonConverter` is stable across versions.

## AOT safety

The converter **type** is resolved at compile time via the `[JsonConverter(typeof(...))]` attribute. The default `JsonSerializer` uses that attribute ahead of any reflection-based discovery, so:

- No reflection over the struct's members.
- No runtime codegen.
- Safe under `PublishAot`, `PublishTrimmed`, and NativeAOT — provided the converter is registered (either implicitly via the attribute on the default path, or explicitly via `options.Converters.Add(...)` on the source-gen path).

## Source-gen contexts (`JsonSerializerContext`)

The source-generated `JsonSerializerContext` pipeline has a known limitation that affects every library shipping generator-emitted converters, not just this one: **the STJ source generator does not observe `[JsonConverter]` attributes emitted by other source generators.** Roslyn runs all generators against the original compilation in parallel, so the `[JsonConverter]` attribute the TypedId generator emits is invisible to STJ's generator. Without intervention, STJ would treat the TypedId as a POCO and serialize it to `{}`.

The fix is one line — register the converter explicitly on the options:

```csharp
[JsonSerializable(typeof(OrderCreated))]
[JsonSerializable(typeof(OrderId))]
[JsonSerializable(typeof(UserId))]
internal partial class AppJsonContext : JsonSerializerContext;

// In your composition root / Program.cs:
var options = new JsonSerializerOptions
{
    Converters =
    {
        new OrderId.TypedIdJsonConverter(),
        new UserId.TypedIdJsonConverter(),
    },
    TypeInfoResolver = AppJsonContext.Default,
};
```

STJ then routes serialization through the runtime-registered converter, which the TypedId generator emits. This works under `PublishAot` and `PublishTrimmed` because the converter type, the parameterless constructor, and the TypedId's `Parse`/`ToString` members are all statically reachable from the registration.

> **Why not just `[JsonSerializable(typeof(OrderId))]`?** Because of the Roslyn limitation above, that alone produces a POCO `JsonTypeInfo` for `OrderId` that ignores the converter. The explicit `Converters.Add(...)` line is what wires up the converter for the source-gen path.

## Serialize and deserialize an order

```csharp
using System.Text.Json;

[TypedId(Strategy = IdStrategy.Ulid)]
public readonly partial record struct OrderId;

public sealed record Order(OrderId Id, decimal Total, DateTimeOffset PlacedAt);

var order = new Order(OrderId.New(), 42.50m, DateTimeOffset.UtcNow);

string json = JsonSerializer.Serialize(order);
// {"Id":"01ARZ3NDEKTSV4RRFFQ69G5FAV","Total":42.5,"PlacedAt":"2026-04-22T10:15:00+00:00"}

Order roundTripped = JsonSerializer.Deserialize<Order>(json)!;
Console.WriteLine(roundTripped.Id == order.Id);   // true
```

## Nested in envelope types

TypedId works inside dictionaries, arrays, and discriminated envelopes:

```csharp
public sealed record EventEnvelope<T>(
    MessageId EventId,
    string Type,
    DateTimeOffset OccurredAt,
    T Payload);

public sealed record OrderCreated(OrderId OrderId, UserId UserId, decimal Total);

var envelope = new EventEnvelope<OrderCreated>(
    MessageId.New(),
    "order.created",
    DateTimeOffset.UtcNow,
    new OrderCreated(OrderId.New(), UserId.New(), 99m));

string json = JsonSerializer.Serialize(envelope);
/*
{
  "EventId": "1748213984512000001",
  "Type": "order.created",
  "OccurredAt": "...",
  "Payload": {
    "OrderId": "01ARZ3NDEKTSV4RRFFQ69G5FAV",
    "UserId":  "01ARZ3NDEKTSV4RRFFQ69G5FAW",
    "Total": 99
  }
}
*/
```

Each TypedId serializes to its native string form: ULID base32, UUIDv7 hyphenated, Snowflake decimal.

## Dictionary keys

TypedIds serialize to string, so they work as JSON dictionary keys:

```csharp
var balances = new Dictionary<UserId, decimal>
{
    [UserId.New()] = 100m,
    [UserId.New()] = 250m,
};

string json = JsonSerializer.Serialize(balances);
// {"01ARZ3...":100,"01ARZ4...":250}
```

## Invalid input

`Deserialize` calls `Parse`, which throws `FormatException` on malformed input. `System.Text.Json` wraps it into `JsonException`:

```csharp
try
{
    JsonSerializer.Deserialize<Order>("""{"Id":"not-a-ulid","Total":0}""");
}
catch (JsonException ex)
{
    // ex.InnerException is FormatException from OrderId.Parse
}
```

Unknown tokens (e.g. a JSON number where a string is expected) also surface as `JsonException`. The converter only accepts `JsonTokenType.String`.

## Null handling

A `[TypedId]` struct is a value type. `null` in JSON maps to `Nullable<OrderId>`:

```csharp
public sealed record MaybeOrder(OrderId? Id);

var parsed = JsonSerializer.Deserialize<MaybeOrder>("""{"Id":null}""");
// parsed.Id == null
```

A non-nullable property deserializing from `"Id": null` throws `JsonException` — standard BCL behaviour for non-nullable value types.

## What's not generated

- No JSON `JsonSerializerOptions` mutation — the library never touches global state.
- No separate JSON package — this is BCL-only.
- No custom naming. `ToString()` / `Parse` is the contract.

For the underlying mechanism see [Internals](internals.md).
