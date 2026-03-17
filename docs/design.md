---
id: design
title: Design Decisions
slug: /docs/design
description: Intentional omissions (no with, no Deconstruct, no IComparable) and the reasoning behind them.
sidebar_position: 13
---

# Design Decisions & Limitations

## What the generator does NOT produce

| Feature | `record` | `ZeroAlloc.ValueObjects` |
|---|---|---|
| `Equals` / `GetHashCode` / `==` / `!=` | ✓ | ✓ |
| `ToString` | ✓ | ✓ |
| `with` expression | ✓ | ✗ |
| `Deconstruct` | ✓ | ✗ |
| `EqualityContract` | ✓ | ✗ |
| `IComparable` / ordering operators | ✗ | ✗ |

These omissions are intentional — the generator is minimal by design.

---

## `sealed partial class`

The generator marks classes as `sealed`. This is intentional:

- Prevents subclasses from inheriting equality semantics and breaking the Liskov principle
- Enables the JIT to devirtualize `Equals` calls in many cases

If your type must be inheritable, implement equality manually.

---

## `readonly partial struct`

Structs are marked `readonly` because mutable value-type equality is a known C# footgun:

```csharp
// Dangerous without readonly — mutating after inserting into a dictionary
// causes the hash bucket to drift, making the value unreachable
var dict = new Dictionary<Point, string>();
var p = new MutablePoint(1, 2);
dict[p] = "here";
p.X = 99;  // hash changes — "here" is now unreachable
```

If you genuinely need a mutable struct value object, implement equality manually.

---

## No `with` expression

Unlike `record`, the generator does not emit a `with`-compatible copy constructor. Value objects are typically immutable; construct a new instance when values change.

```csharp
// record: var updated = original with { Currency = "EUR" };
// ZeroAlloc: construct a new instance explicitly
var updated = new Money(original.Amount, "EUR");
```

---

## No `Deconstruct`

`record` emits `Deconstruct`. The generator does not. Add one manually if needed:

```csharp
[ValueObject]
public partial class Money
{
    public decimal Amount { get; }
    public string Currency { get; }

    public void Deconstruct(out decimal amount, out string currency)
    {
        amount = Amount;
        currency = Currency;
    }
}

// Usage
var (amount, currency) = new Money(100m, "USD");
```

---

## No `IComparable` support

The generator only produces equality. Ordering (`IComparable<T>`, `operator <`, `operator >`) must be implemented manually:

```csharp
[ValueObject]
public partial class Money : IComparable<Money>
{
    public decimal Amount { get; }
    public string Currency { get; }

    public int CompareTo(Money? other)
    {
        if (other is null) return 1;
        if (Currency != other.Currency) throw new InvalidOperationException("Cannot compare different currencies");
        return Amount.CompareTo(other.Amount);
    }

    public static bool operator <(Money left, Money right)  => left.CompareTo(right) < 0;
    public static bool operator >(Money left, Money right)  => left.CompareTo(right) > 0;
    public static bool operator <=(Money left, Money right) => left.CompareTo(right) <= 0;
    public static bool operator >=(Money left, Money right) => left.CompareTo(right) >= 0;
}
```

---

## Only public properties with getters

Private, internal, protected, or write-only properties are never included. Static properties are never included. This reflects the principle that value object equality should mirror observable public state.
