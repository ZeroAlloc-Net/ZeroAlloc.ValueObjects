---
id: generated-output
title: Generated Output
slug: /docs/generated-output
description: Exact code the source generator emits for Equals, GetHashCode, and ToString.
sidebar_position: 7
---

# What Gets Generated

For every `[ValueObject]` type, the generator emits a partial declaration alongside yours in a `.g.cs` file.

## Class output

```csharp
// Input
[ValueObject]
public partial class Money
{
    public decimal Amount { get; }
    public string Currency { get; }
}

// Generated (Money.g.cs)
sealed partial class Money : System.IEquatable<Money>
{
    public override bool Equals(object? obj) =>
        obj is Money other && Equals(other);

    public bool Equals(Money? other) =>
        other is not null &&
        Amount == other.Amount &&
        Currency == other.Currency;

    public override int GetHashCode() =>
        System.HashCode.Combine(Amount, Currency);

    public static bool operator ==(Money? left, Money? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(Money? left, Money? right) =>
        !(left == right);

    public override string ToString() =>
        $"Money {{ Amount = {Amount}, Currency = {Currency} }}";
}
```

## Struct output

```csharp
// Input
[ValueObject]
public partial struct CustomerId
{
    public int Value { get; }
}

// Generated
readonly partial struct CustomerId : System.IEquatable<CustomerId>
{
    public override bool Equals(object? obj) =>
        obj is CustomerId other && Equals(other);

    public bool Equals(CustomerId other) =>    // no nullable — structs can't be null
        Value == other.Value;

    public override int GetHashCode() =>
        Value.GetHashCode();                   // one member: no HashCode.Combine

    public static bool operator ==(CustomerId left, CustomerId right) =>
        left.Equals(right);                    // no null guard needed

    public static bool operator !=(CustomerId left, CustomerId right) =>
        !left.Equals(right);

    public override string ToString() =>       // one member: its value alone
        Value.ToString(global::System.Globalization.CultureInfo.InvariantCulture);
}
```

## Nested and generic output

A nested value object is generated inside partial declarations of its containing types, outermost first, so every containing type must be `partial`; otherwise the generator reports [ZAVO001](diagnostics.md#zavo001). A generic value object keeps its type parameters, and a member whose type is a type parameter compares through `EqualityComparer<T>.Default`:

```csharp
// Input
public partial class Orders
{
    [ValueObject]
    public partial class Line<T>
    {
        public T Item { get; }
        public int Quantity { get; }
    }
}

// Generated
partial class Orders
{
sealed partial class Line<T> : System.IEquatable<Line<T>>
{
    public bool Equals(Line<T>? other) =>
        other is not null &&
        global::System.Collections.Generic.EqualityComparer<T>.Default.Equals(Item, other.Item) &&
        Quantity == other.Quantity;
    // ... the same members as above
}
}
```

## Hash code strategy

| Number of properties | Strategy |
|---|---|
| 0 | `return 0;` |
| 1 | `p1.GetHashCode()`, null-safe for reference types |
| 2–8 | `System.HashCode.Combine(p1, p2, ...)` |
| 9+ | `var hc = new System.HashCode(); hc.Add(p1); ... return hc.ToHashCode();` |

## Type modifiers

| Type | Generated modifier |
|---|---|
| `partial class` | `sealed partial class` |
| `partial struct` | `readonly partial struct` |
| `partial struct` with `ForceClass = true` | `sealed partial class` |

Classes are `sealed` to prevent subclasses from accidentally inheriting equality semantics and to allow the JIT to devirtualize `Equals` calls.

Structs are `readonly` because mutable value-type equality is a known footgun in C#.
