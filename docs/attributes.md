# Attribute Reference

## `[ValueObject]`

Applied to a `partial class` or `partial struct`. Triggers generation of all equality members.

```csharp
[ValueObject]
public partial class OrderId { ... }

[ValueObject]
public partial struct Quantity { ... }
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

## Summary table

| Attribute | Target | Effect |
|---|---|---|
| `[ValueObject]` | `partial class` or `partial struct` | Triggers code generation |
| `[ValueObject(ForceClass = true)]` | `partial struct` | Generates a `sealed class` instead of `readonly struct` |
| `[EqualityMember]` | Property | Opt-in: only marked props are compared |
| `[IgnoreEqualityMember]` | Property | Opt-out: all props except marked ones are compared |
