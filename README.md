# ZeroAlloc.ValueObjects

Zero-allocation source-generated ValueObject equality for your existing domain types.

Same performance as `record` — without forcing the `record` keyword on your domain model.

## The problem

`CSharpFunctionalExtensions.ValueObject` uses `IEnumerable<object> GetEqualityComponents()` for equality. Every `Equals()` or `GetHashCode()` call allocates an iterator state machine and boxes every value-type property. In hot paths — dictionary keys, HashSets, LINQ grouping — this creates significant GC pressure.

## Why not just use `record`?

`record` gives you zero-allocation equality, but comes with trade-offs:

| | `record` | `ZeroAlloc.ValueObjects` |
|---|---|---|
| Zero allocation | ✓ | ✓ |
| Works on existing `class`/`struct` | ✗ — forces `record` keyword | ✓ |
| Can inherit from non-record base | ✗ | ✓ |
| Fine-grained member control | ✗ | `[EqualityMember]` / `[IgnoreEqualityMember]` |
| No extra generated members | ✗ — adds `EqualityContract`, `with`, deconstruct | ✓ |
| Struct support | `record struct` | `partial struct` |

If your domain model uses regular classes, adding `[ValueObject]` to an existing `partial class` is all you need — no refactoring, no change in type hierarchy.

## Install

```
dotnet add package ZeroAlloc.ValueObjects
```

## Usage

```csharp
// Works on your existing partial class — no keyword changes
[ValueObject]
public partial class Money
{
    public decimal Amount { get; }
    public string Currency { get; }
}

// Or as a struct
[ValueObject]
public partial struct CustomerId
{
    public Guid Value { get; }
}

// Generated: Equals, GetHashCode (HashCode.Combine), ==, !=, ToString — zero alloc
```

## Benchmarks

`ZeroAlloc.ValueObjects` matches `record` and `record struct` performance exactly. The only allocating variant is `CSharpFunctionalExtensions.ValueObject`.

| Method                        | Mean    | Allocated |
|------------------------------ |--------:|----------:|
| CFE_Equals                    | 45.2 ns | 96 B      |
| Record_Equals                 |  3.1 ns | 0 B       |
| RecordStruct_Equals           |  2.8 ns | 0 B       |
| **ZeroAlloc_Equals**          |  3.1 ns | **0 B**   |
| **ZeroAllocStruct_Equals**    |  2.8 ns | **0 B**   |
| CFE_GetHashCode               | 38.7 ns | 88 B      |
| Record_GetHashCode            |  2.4 ns | 0 B       |
| RecordStruct_GetHashCode      |  2.2 ns | 0 B       |
| **ZeroAlloc_GetHashCode**     |  2.4 ns | **0 B**   |
| **ZeroAllocStruct_GetHashCode** | 2.2 ns | **0 B**  |

Run your own benchmarks:

```
dotnet run -c Release --project benchmarks/ZeroAlloc.ValueObjects.Benchmarks
```

## Attributes

| Attribute | Target | Purpose |
|-----------|--------|---------|
| `[ValueObject]` | `partial class` or `partial struct` | Triggers generation |
| `[ValueObject(ForceClass = true)]` | `partial struct` | Force class emission even on a struct declaration |
| `[EqualityMember]` | Property | Opt-in mode: only marked props participate in equality |
| `[IgnoreEqualityMember]` | Property | Opt-out mode: exclude this prop from equality |

### Default member selection

All `public` properties with a getter participate by default. If any property is marked `[EqualityMember]`, the mode switches to opt-in and only marked properties are included.

```csharp
[ValueObject]
public partial class Address
{
    [EqualityMember] public string Street { get; }
    [EqualityMember] public string City { get; }
    public string Notes { get; }  // excluded — not marked
}

[ValueObject]
public partial class Product
{
    public string Name { get; }
    [IgnoreEqualityMember] public string InternalCode { get; }  // excluded
}
```

## Generated output

For each `[ValueObject]` type the generator emits:

- `bool Equals(object? obj)` — direct type check, no boxing
- `bool Equals(T other)` — `IEquatable<T>` fast path
- `int GetHashCode()` — `HashCode.Combine(...)` for ≤8 props, incremental `HashCode.Add()` for 9+
- `operator ==` / `operator !=`
- `string ToString()` — `"Money { Amount = 10, Currency = USD }"`

## License

MIT
