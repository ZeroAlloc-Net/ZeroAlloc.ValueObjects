---
id: attributes
title: Attribute Reference
slug: /docs/attributes
description: Complete reference for [ValueObject], [ValueObjectFactory], [EqualityMember], and [IgnoreEqualityMember].
sidebar_position: 5
---

# Attribute Reference

## `[ValueObject]`

Applied to a `partial class` or `partial struct`. Triggers generation of all equality members.

```csharp
[ValueObject]
public partial class OrderId { ... }

[ValueObject]
public partial struct Quantity { ... }
```

The type can be generic, and it can be nested in another type. A nested value object needs every containing type to be `partial` too, because the generated code reopens them; otherwise the generator reports [ZAVO001](diagnostics.md#zavo001) and generates nothing for it. A member whose type is a type parameter compares through `EqualityComparer<T>.Default`.

```csharp
public partial class Orders
{
    [ValueObject]
    public partial class Line
    {
        public int Quantity { get; }
    }
}

[ValueObject]
public partial struct Range<T>
{
    public T Low { get; }
    public T High { get; }
}
```

### Property: `ForceClass` (bool, default `false`)

When your declaration is a `struct` but you need reference semantics, set `ForceClass = true`. The generator emits a `sealed partial class` instead of a `readonly partial struct`.

```csharp
// Declared as struct, generated as sealed class
[ValueObject(ForceClass = true)]
public partial struct LegacyId
{
    public int Value { get; }
}
```

---

## `[EqualityMember]`

Applied to a **property**. Switches the type into **opt-in mode**: only properties marked `[EqualityMember]` participate in equality. All other properties are ignored.

Opt-in mode activates the moment **any** property in the type carries this attribute.

```csharp
[ValueObject]
public partial class Address
{
    [EqualityMember] public string Street { get; }
    [EqualityMember] public string City { get; }
    [EqualityMember] public string PostalCode { get; }

    // Intentionally excluded from equality
    public string? Alias { get; }
    public DateTime LastUpdated { get; }
}
```

Two addresses with identical `Street`/`City`/`PostalCode` but different `Alias` values are equal.

---

## `[IgnoreEqualityMember]`

Applied to a **property**. All public properties with getters participate in equality **except** those marked with this attribute (opt-out mode).

```csharp
[ValueObject]
public partial class Product
{
    public string Sku { get; }
    public string Name { get; }

    // Internal tracking — irrelevant to domain equality
    [IgnoreEqualityMember] public string InternalCode { get; }
    [IgnoreEqualityMember] public Guid AuditId { get; }
}
```

Two products with the same `Sku` and `Name` are equal regardless of `InternalCode` or `AuditId`.

---

## `[ValueObjectFactory]`

Applied to a `partial class` or `partial struct` next to `[ValueObject]`. It names the static factory that builds the type from its underlying value when the value comes from outside, such as a serializer reading untrusted input. ValueObjects itself does not read it; consumers such as [ZeroAlloc.Serialisation](https://github.com/ZeroAlloc-Net/ZeroAlloc.Serialisation) do.

```csharp
[ValueObject]
[ValueObjectFactory(nameof(TryCreate))]
public readonly partial struct Sku
{
    public string Value { get; }
    private Sku(string value) => Value = value;

    public static bool TryCreate(string? input, out Sku sku) { ... }
}
```

The named method must be static, accessible to the consumer, and have one of two shapes:

- `static bool TryCreate(TUnderlying, out T)`: `false` rejects the value.
- `static T Create(TUnderlying)`: throwing rejects the value.

Without the attribute, a consumer that supports the convention looks for a method called `TryCreate` first and `Create` second. The attribute overrides that convention, for example when the factory has another name or when both exist and `Create` should win.

---

## Summary table

| Attribute | Target | Effect |
|---|---|---|
| `[ValueObject]` | `partial class` or `partial struct` | Triggers code generation |
| `[ValueObject(ForceClass = true)]` | `partial struct` | Generates a `sealed class` instead of `readonly struct` |
| `[EqualityMember]` | Property | Opt-in: only marked props are compared |
| `[IgnoreEqualityMember]` | Property | Opt-out: all props except marked ones are compared |
| `[ValueObjectFactory(name)]` | `partial class` or `partial struct` | Names the static factory consumers use to build the type from an underlying value |
