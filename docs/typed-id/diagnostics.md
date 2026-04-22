---
id: typed-id-diagnostics
title: TypedId Diagnostics
slug: /docs/typed-id/diagnostics
description: ZATI001–ZATI005 reference with example code and fixes.
sidebar_position: 27
---

# Diagnostics

The generator emits five diagnostic IDs, `ZATI001` through `ZATI005`. Each is raised during compilation, so invalid TypedId declarations never reach runtime.

## Summary

| ID | Severity | Meaning | Fix |
|---|---|---|---|
| [ZATI001](#zati001) | Error | Incompatible strategy/backing | Use the correct `BackingType` or remove the explicit `Backing` |
| [ZATI002](#zati002) | Error | Type is not `readonly partial record struct` | Add the missing modifiers |
| [ZATI003](#zati003) | Error | Struct body declares fields or properties | Remove the declarations — generator owns `Value` |
| [ZATI004](#zati004) | — | Reserved (enforced by `AttributeUsage`) | N/A |
| [ZATI005](#zati005) | Warning | Struct declared `partial` across multiple files | Consolidate into one file |

---

## ZATI001

**Error.** The `Strategy` and `Backing` combination is incompatible.

Valid combinations:

| Strategy | Valid `BackingType` |
|---|---|
| `Ulid`, `Uuid7` | `Default`, `Guid` |
| `Snowflake`, `Sequential` | `Default`, `Int64` |

### Example — offending code

```csharp
// ZATI001: "Snowflake requires Int64; Guid is incompatible."
[TypedId(Strategy = IdStrategy.Snowflake, Backing = BackingType.Guid)]
public readonly partial record struct MessageId;
```

### Fix

Drop the explicit `Backing` (the generator auto-picks) or specify the matching type:

```csharp
// Auto-picks Int64
[TypedId(Strategy = IdStrategy.Snowflake)]
public readonly partial record struct MessageId;

// Or be explicit
[TypedId(Strategy = IdStrategy.Snowflake, Backing = BackingType.Int64)]
public readonly partial record struct MessageId;
```

---

## ZATI002

**Error.** The target type is not `readonly partial record struct`. TypedId generation requires all three modifiers plus `record struct`:

- `readonly` — the generator emits a `get`-only `Value` property.
- `partial` — the generator contributes the body in a sibling partial declaration.
- `record struct` — provides free structural equality + hash over the `Value` field.

### Example — offending code

```csharp
// ZATI002: "[TypedId] requires `readonly partial record struct`; OrderId is `partial struct`."
[TypedId]
public partial struct OrderId;
```

```csharp
// ZATI002: "[TypedId] requires `readonly partial record struct`; OrderId is `partial class`."
[TypedId]
public partial class OrderId;
```

### Fix

```csharp
[TypedId]
public readonly partial record struct OrderId;
```

---

## ZATI003

**Error.** The struct declares fields or properties in its body. The generator owns the `Value` field; user-declared members would conflict with the emitted code.

### Example — offending code

```csharp
// ZATI003: "[TypedId] structs must have an empty body; generator emits the `Value` field."
[TypedId]
public readonly partial record struct OrderId
{
    public Guid InternalValue { get; }    // conflicts
}
```

### Fix

Leave the body empty. If you need supplementary methods, add them in a **separate** partial declaration (the warning about that is [ZATI005](#zati005)):

```csharp
[TypedId]
public readonly partial record struct OrderId;

// In a separate file — or still in the same file, just in another partial block
public readonly partial record struct OrderId
{
    public bool IsBefore(OrderId other) => this.CompareTo(other) < 0;
}
```

Methods and computed read-only properties in a sibling partial declaration are fine. **Fields** in any partial declaration trigger ZATI003.

---

## ZATI004

**Reserved.** No generator diagnostic is emitted. Instead, `TypedIdDefaultAttribute` carries `[AttributeUsage(AttributeTargets.Assembly)]`, so applying it to a non-assembly target is a standard C# compiler error:

```csharp
// CS0592: Attribute 'TypedIdDefault' is not valid on this declaration type.
// It is only valid on 'assembly' declarations.
[TypedIdDefault(Strategy = IdStrategy.Ulid)]
public class Program { }
```

The fix is to move it to an assembly-level location:

```csharp
[assembly: TypedIdDefault(Strategy = IdStrategy.Ulid)]
```

ZATI004 is kept reserved in case a future runtime validation needs the slot.

---

## ZATI005

**Warning.** The struct is declared `partial` across multiple source files. Partial declarations are legal C#, but splitting a TypedId across files creates drift risk — a future edit in one file that violates ZATI002/ZATI003 might not be obvious from the other.

### Example — offending code

```csharp
// File A.cs
[TypedId]
public readonly partial record struct OrderId;

// File B.cs
public readonly partial record struct OrderId
{
    public bool IsEpochId() => Value == Guid.Empty;
}
```

This compiles and runs, but you get `ZATI005` as a warning.

### Fix

Consolidate into one file:

```csharp
// OrderId.cs
[TypedId]
public readonly partial record struct OrderId
{
    public bool IsEpochId() => Value == Guid.Empty;
}
```

Or suppress locally if you intentionally split (e.g. for code-organization reasons):

```csharp
#pragma warning disable ZATI005
public readonly partial record struct OrderId { /* extras */ }
#pragma warning restore ZATI005
```

---

## Turning diagnostics into errors

Standard .NET knobs apply. To fail the build on ZATI005:

```xml
<PropertyGroup>
  <WarningsAsErrors>ZATI005</WarningsAsErrors>
</PropertyGroup>
```

Or suppress globally:

```xml
<PropertyGroup>
  <NoWarn>$(NoWarn);ZATI005</NoWarn>
</PropertyGroup>
```

See also [Production checklist](production.md) for runtime-time guardrails.
