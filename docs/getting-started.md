---
id: getting-started
title: Getting Started
slug: /docs/getting-started
description: Install, annotate a class, and get zero-allocation equality in three steps.
sidebar_position: 3
---

# Getting Started

## How the generator pipeline works

```mermaid
flowchart LR
    A([Your partial class/struct]) --> B[Roslyn\nIncremental Generator]
    B --> C{Detects\n[ValueObject]}
    C -->|No| D([No output])
    C -->|Yes| E[ValueObjectParser\nanalyzes properties]
    E --> F{Property\nattributes?}
    F -->|EqualityMember\non any prop| G[Opt-in mode:\nonly marked props]
    F -->|IgnoreEqualityMember\non some props| H[Opt-out mode:\nall except marked]
    F -->|None| I[Default mode:\nall public props]
    G & H & I --> J[SourceWriter\nemits .g.cs]
    J --> K([Equals / GetHashCode\n== / != / ToString])
```

## Step 1 — Mark your type as `partial`

The generator emits code into a second partial declaration alongside yours. Your type must be declared `partial`.

```csharp
// Before
public class Money { ... }

// After
public partial class Money { ... }
```

## Step 2 — Add `[ValueObject]`

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

## Step 3 — Use it

```csharp
var usd100  = new Money(100m, "USD");
var usd100b = new Money(100m, "USD");
var eur50   = new Money(50m, "EUR");

Console.WriteLine(usd100 == usd100b);   // True
Console.WriteLine(usd100 == eur50);     // False
Console.WriteLine(usd100.GetHashCode() == usd100b.GetHashCode());  // True

// Works as dictionary key, HashSet element, etc.
var prices = new Dictionary<Money, string>();
prices[usd100] = "one hundred dollars";
Console.WriteLine(prices[usd100b]);     // "one hundred dollars"
```

## What "zero allocation" means

The generated equality code uses only stack-local comparisons. For a two-property class:

```csharp
// Generated — no heap allocations
public bool Equals(Money? other) =>
    other is not null &&
    Amount == other.Amount &&
    Currency == other.Currency;

public override int GetHashCode() =>
    System.HashCode.Combine(Amount, Currency);
```

There is no iterator, no boxing, no array creation. Every call is a direct property comparison.
