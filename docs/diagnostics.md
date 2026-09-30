---
id: diagnostics
title: Value Object Diagnostics
slug: /docs/diagnostics
description: ZAVO001–ZAVO003 reference with example code and fixes.
sidebar_position: 16
---

# Value Object Diagnostics

The `[ValueObject]` generator emits three diagnostic IDs, `ZAVO001` through `ZAVO003`. Each one names a value object the generator cannot generate, and nothing is generated for that value object. Every other value object in the project is generated as usual. `[TypedId]` has its own IDs; see [TypedId diagnostics](typed-id/diagnostics.md).

| ID | Severity | Meaning | Fix |
|---|---|---|---|
| [ZAVO001](#zavo001) | Warning | Nested value object inside a containing type that is not `partial` | Make every containing type `partial` |
| [ZAVO002](#zavo002) | Error | File-local value object | Remove the `file` modifier |
| [ZAVO003](#zavo003) | Error | Value object name differs only in case from another value object | Rename one of them |

A value object can be declared anywhere a class or struct can: at the top of a namespace, in the global namespace, nested in another type, or generic, such as `Range<T>`. A generic value object is generated with its type parameters, and an equality member whose type is a type parameter compares through `EqualityComparer<T>.Default`, because `==` is not defined for an unconstrained type parameter.

---

## ZAVO001

**Warning.** `Value object 'App.Outer.Money' is not generated because its containing type 'App.Outer' is not partial`.

A nested value object is generated inside partial declarations of every containing type, so each of them must be `partial`. When one is not, the generator reports ZAVO001 on the value object and generates nothing for it. The message names the outermost containing type that is not `partial`.

Nothing is generated, so the value object keeps reference equality, and its `==` operator does not exist. Earlier versions generated such a value object into a new top-level type with its simple name, which did not compile.

### Example — offending code

```csharp
public class Outer
{
    [ValueObject]
    public partial class Money
    {
        public decimal Amount { get; }
    }
}
```

### Fix

Make every containing type `partial`, or move the value object to the top level of a namespace.

```csharp
public partial class Outer
{
    [ValueObject]
    public partial class Money
    {
        public decimal Amount { get; }
    }
}
```

Under `TreatWarningsAsErrors` this warning fails the build.

---

## ZAVO002

**Error.** `Value object 'App.Money' is not generated because 'App.Money' is file-local`.

A `file` type is visible only in the file that declares it, so the generated file cannot reopen it. The same applies to a value object nested in a `file` type; the message then names that type.

### Example — offending code

```csharp
[ValueObject]
file partial class Money
{
    public decimal Amount { get; }
}
```

### Fix

Remove the `file` modifier. Use `internal` to keep the type out of the public surface.

---

## ZAVO003

**Error.** `Value object 'App.money' is not generated because its file name 'App.money.g.cs' differs only in case from that of 'App.Money'`.

Each value object is generated into a file named after its namespace, containing types and name. Roslyn compares those file names ignoring case, so two value objects whose qualified names differ only in case, such as `App.Money` and `App.money`, would need the same file. The one declared first, by file path and then position, is generated. Every later one gets ZAVO003 and is not generated.

Earlier versions failed with CS8785 in this case, and generated no value object in the project at all.

### Example — offending code

```csharp
namespace App;

[ValueObject] public partial class Money { public decimal Amount { get; } }
[ValueObject] public partial class money { public decimal Amount { get; } }
```

### Fix

Rename one of the value objects, or move it to another namespace, so the qualified names differ in more than case.

---

## Release tracking

New rules are recorded in `src/ZeroAlloc.ValueObjects.Generator/AnalyzerReleases.Unshipped.md`. See [TypedId diagnostics](typed-id/diagnostics.md#release-tracking) for how they move to the shipped file on release.
