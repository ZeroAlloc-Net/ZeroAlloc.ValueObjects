---
id: typed-id-aspnet
title: ASP.NET Minimal API Binding
slug: /docs/typed-id/aspnet
description: Route and query binding for TypedId via generated IParsable and ISpanParsable implementations.
sidebar_position: 25
---

# ASP.NET Minimal API Binding

Every `[TypedId]` struct implements `IParsable<T>` and `ISpanParsable<T>`. ASP.NET Core's minimal-API model binder looks for those interfaces first, so route tokens, query strings, and header values bind to TypedIds with no registration.

## Route binding

```csharp
using ZeroAlloc.ValueObjects;

[TypedId(Strategy = IdStrategy.Ulid)]
public readonly partial record struct OrderId;

var app = WebApplication.Create(args);

app.MapGet("/orders/{id}", (OrderId id) =>
    $"Looking up order {id}");

app.Run();
```

Requests:

```
GET /orders/01ARZ3NDEKTSV4RRFFQ69G5FAV
  → 200 "Looking up order 01ARZ3NDEKTSV4RRFFQ69G5FAV"

GET /orders/not-a-ulid
  → 400 Bad Request
      "Failed to bind parameter 'OrderId id' from 'not-a-ulid'."
```

No `MapGet` setup, no converter, no endpoint filter.

## Query binding

`[FromQuery]` follows the same rules:

```csharp
app.MapGet("/orders", ([FromQuery] OrderId? after, [FromQuery] int take = 50) =>
{
    // paginate starting after `after`
});
```

Requests:

```
GET /orders?after=01ARZ3NDEKTSV4RRFFQ69G5FAV&take=25   → 200
GET /orders?take=25                                     → 200, after = null
GET /orders?after=garbage                               → 400 Bad Request
```

Note the `OrderId?` — value types must be declared nullable to participate in optional-query semantics.

## Multiple IDs in one route

```csharp
app.MapGet("/users/{userId}/orders/{orderId}",
    (UserId userId, OrderId orderId) => /* ... */);
```

Each segment binds independently — mixing UUIDv7 and ULID in one route works.

## Pairing with `[FromBody]`

Route-bound TypedIds compose naturally with JSON-bound request bodies:

```csharp
public sealed record UpdateOrderRequest(decimal Total, string Notes);

app.MapPut("/orders/{id}",
    async (OrderId id, UpdateOrderRequest body, IOrderService svc) =>
    {
        await svc.UpdateAsync(id, body);
        return Results.NoContent();
    });
```

The route token drives `id` via `IParsable<OrderId>.TryParse`. The body drives `body` via the JSON converter described in [JSON serialization](json.md).

## MVC controllers

Classic MVC also honours `IParsable<T>` as of ASP.NET Core 7+:

```csharp
[ApiController]
[Route("orders")]
public class OrdersController : ControllerBase
{
    [HttpGet("{id}")]
    public IActionResult Get(OrderId id) => Ok(id);
}
```

Older ASP.NET Core versions rely on `TypeConverter` for classic MVC model binding; that is out of scope for v1. If you need it, wrap the TypedId in a one-line custom `TypeConverter` and register it locally.

## Invalid input behaviour

`TryParse` returns `false` for any string the strategy can't decode. The minimal-API binder translates that into a 400 with a framework-standard message:

```
Failed to bind parameter "OrderId id" from "bad-value".
```

To customize the response, add an endpoint filter or exception handler. The library itself never throws from `TryParse` — it only returns `false`.

## Performance

Both interfaces have zero-allocation implementations:

- `ISpanParsable<T>.TryParse(ReadOnlySpan<char>, ...)` decodes the span directly into the backing value without allocating an intermediate string.
- The ASP.NET binder calls `ISpanParsable` when the route value is already a span, so typical request binding allocates nothing beyond what the framework itself does.

`IParsable<T>.Parse(string, ...)` delegates to the span overload after `AsSpan()` — also zero-alloc.

## No setup required

Unlike custom `IModelBinder` registrations or `ValueConverter` pipelines, TypedId route binding needs no `builder.Services.Add…` call. The BCL's model binder discovery does the rest.

For the full generated shape see [Internals](internals.md).
