---
id: migration
title: Migration Guide
slug: /docs/migration
description: Upgrading from 2.x to 3.0, and migrating from CSharpFunctionalExtensions.ValueObject or from manual equality implementations.
sidebar_position: 11
---

# Migration Guide

## From 2.x to 3.0

- **Target framework.** Every package, `ZeroAlloc.ValueObjects`, `ZeroAlloc.ValueObjects.EfCore` and `ZeroAlloc.ValueObjects.Hosting`, ships `net10.0` and `net11.0` assets only. Version 2.x also ran on `netstandard2.0`, `netstandard2.1`, .NET 8 and .NET 9, which are out of support. Move the project to .NET 10 or later, or stay on 2.x.
- **ZAVO001 and ZATI006 are errors.** A `[ValueObject]` or `[TypedId]` type nested in a containing type that is not `partial` now fails the build instead of warning; nothing was generated for it before either. Make every containing type `partial`, or move the type to the top level of a namespace. See [ZAVO001](diagnostics.md#zavo001) and [ZATI006](typed-id/diagnostics.md#zati006).
- **The generated `TypedIdSerializer` is unconditional.** It was wrapped in `#if NETSTANDARD2_1_OR_GREATER || NET`; every target framework 3.0 supports defines `NET`, so the generated code is the same.

## From `CSharpFunctionalExtensions.ValueObject`

### Step-by-step

```mermaid
flowchart LR
    A([Add ZeroAlloc\nNuGet package]) --> B([Add partial\nto class])
    B --> C([Replace base class\nwith attribute])
    C --> D([Remove\nGetEqualityComponents])
    D --> E([Add EqualityMember or\nIgnoreEqualityMember\nif needed])
    E --> F([Build])
```

### Before

```csharp
using CSharpFunctionalExtensions;

public class Money : ValueObject
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    protected override IEnumerable<IComparable> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }
}
```

### After

```csharp
using ZeroAlloc.ValueObjects;

[ValueObject]
public partial class Money        // 1. add partial
{                                 // 2. remove : ValueObject
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }
                                  // 3. remove GetEqualityComponents()
}
```

### Handling partial equality

If `GetEqualityComponents()` previously returned fewer properties than the class has, map that to attributes:

```csharp
// Before — only Name was used for equality
public class Category : ValueObject
{
    public string Name { get; }
    public string Description { get; }  // not in GetEqualityComponents

    protected override IEnumerable<IComparable> GetEqualityComponents()
    {
        yield return Name;
    }
}

// After — use [EqualityMember] opt-in
[ValueObject]
public partial class Category
{
    [EqualityMember] public string Name { get; }
    public string Description { get; }  // excluded automatically
}

// Or use [IgnoreEqualityMember] opt-out
[ValueObject]
public partial class Category
{
    public string Name { get; }
    [IgnoreEqualityMember] public string Description { get; }
}
```

---

## From Manual Equality Implementation

### Before

```csharp
public class ProductCode : IEquatable<ProductCode>
{
    public string Value { get; }

    public ProductCode(string value) => Value = value;

    public override bool Equals(object? obj) =>
        obj is ProductCode other && Equals(other);

    public bool Equals(ProductCode? other) =>
        other is not null && Value == other.Value;

    public override int GetHashCode() =>
        HashCode.Combine(Value);

    public static bool operator ==(ProductCode? left, ProductCode? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(ProductCode? left, ProductCode? right) =>
        !(left == right);
}
```

### After

```csharp
using ZeroAlloc.ValueObjects;

[ValueObject]
public partial class ProductCode
{
    public string Value { get; }
    public ProductCode(string value) => Value = value;
    // Equality members removed — generated automatically with identical semantics
}
```

Delete the hand-written `Equals`, `GetHashCode`, `operator ==`, `operator !=`. The generator produces equivalent code.
