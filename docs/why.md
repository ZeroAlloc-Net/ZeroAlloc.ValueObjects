---
id: why
title: Why This Library Exists
slug: /docs/why
description: The allocation problem with CSharpFunctionalExtensions.ValueObject and how ZeroAlloc solves it.
sidebar_position: 2
---

# Why This Library Exists

## The allocation problem with `CSharpFunctionalExtensions.ValueObject`

The popular `CSharpFunctionalExtensions` library provides a `ValueObject` base class using this pattern:

```csharp
public class Money : ValueObject
{
    public decimal Amount { get; }
    public string Currency { get; }

    protected override IEnumerable<IComparable> GetEqualityComponents()
    {
        yield return Amount;    // boxes decimal → object
        yield return Currency;
    }
}
```

Every `Equals()` or `GetHashCode()` call:
1. Allocates an iterator state machine object on the heap
2. Boxes every value-type property (`decimal`, `int`, `Guid`, …) into an `object`

In hot paths — dictionary lookups, HashSet operations, LINQ grouping — this creates continuous GC pressure. At scale, this means more frequent garbage collections and longer pauses.

## Why not just use `record`?

`record` solves the allocation problem but has trade-offs:

| | `record` | `ZeroAlloc.ValueObjects` |
|---|---|---|
| Zero allocation | ✓ | ✓ |
| Works on existing `class` | ✗ — forces `record` keyword | ✓ |
| Inherits from non-record base | ✗ | ✓ |
| Fine-grained member control | ✗ | ✓ via `[EqualityMember]` / `[IgnoreEqualityMember]` |
| No extra generated members | ✗ — adds `EqualityContract`, `with`, deconstruct | ✓ |
| Struct support | `record struct` only | any `partial struct` |

If your domain model already uses regular classes — especially when inheriting from a base like `Entity`, `DomainEvent`, or a third-party type — switching to `record` is invasive. `[ValueObject]` on a `partial class` requires no type hierarchy changes.
