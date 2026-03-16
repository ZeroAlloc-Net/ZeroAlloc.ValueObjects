# Troubleshooting

## "Type must be partial"

```
error CS0260: Missing partial modifier on declaration of type 'Money'
```

Add `partial` to your type declaration:

```csharp
// Wrong
[ValueObject]
public class Money { ... }

// Correct
[ValueObject]
public partial class Money { ... }
```

---

## Generated code not appearing

1. Ensure the `ZeroAlloc.ValueObjects` NuGet package is installed (not just referenced as a project without the correct metadata)
2. Rebuild the project (`Build → Rebuild Solution` or `dotnet build`)
3. In Visual Studio, check **Analyzers** under the project's Dependencies node to confirm the generator is loaded
4. Verify the type is `partial` and the attribute namespace is `ZeroAlloc.ValueObjects`

---

## Equality not working as expected

If `==` returns `false` for two objects you expect to be equal, check:

**1. Are the right properties included?**

Use `[EqualityMember]` to be explicit:

```csharp
[ValueObject]
public partial class Product
{
    [EqualityMember] public string Sku { get; }
    [EqualityMember] public string Name { get; }
    // Other props excluded
}
```

**2. Are properties normalized in the constructor?**

If one value is `"USD"` and another is `"usd"`, they are not equal. Normalize in the constructor:

```csharp
public Money(decimal amount, string currency)
{
    Amount = amount;
    Currency = currency.ToUpperInvariant();  // normalize
}
```

**3. Are nested reference-type properties using value equality?**

If a property is itself a class without overridden equality, the generated comparison falls back to reference equality. Ensure nested types also use `[ValueObject]` or otherwise implement value equality.

```csharp
[ValueObject]
public partial class Shipment
{
    public Money Cost { get; }      // Money also has [ValueObject] — uses its Equals()
    public string Carrier { get; }
}
```

---

## `ToString` output is not what I want

The generated `ToString` follows the `record` convention: `TypeName { Prop1 = val1, Prop2 = val2 }`. Override it in your partial declaration:

```csharp
[ValueObject]
public partial class Money
{
    public decimal Amount { get; }
    public string Currency { get; }

    // Overrides the generated version
    public override string ToString() => $"{Amount:F2} {Currency}";
}
```

---

## Struct is generated as a class (or vice versa)

Check the `ForceClass` property:

- `[ValueObject]` on a `partial struct` → generates `readonly partial struct`
- `[ValueObject(ForceClass = true)]` on a `partial struct` → generates `sealed partial class`
- `[ValueObject]` on a `partial class` → generates `sealed partial class`

---

## Compiler warning: `CS0659` or `CS0661`

These warnings appear when you override only one of `Equals`/`GetHashCode` or only `operator ==`/`operator !=`. They should not occur with the generator since it always emits the full set. If you see them, check that you haven't added a partial `Equals` or `GetHashCode` that conflicts with the generated one.
