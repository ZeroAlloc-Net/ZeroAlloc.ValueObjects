# ZeroAlloc.ValueObjects

Zero-allocation source-generated ValueObject equality. Drop-in for `CSharpFunctionalExtensions.ValueObject` hot paths.

## Install

```
dotnet add package ZeroAlloc.ValueObjects
```

## Usage

```csharp
[ValueObject]
public partial class Money
{
    public decimal Amount { get; }
    public string Currency { get; }
}

// Generated: Equals, GetHashCode (HashCode.Combine), ==, !=, ToString — zero alloc
```

## Benchmarks

| Method                    | Mean    | Allocated |
|-------------------------- |--------:|----------:|
| CFE_Equals                | 45.2 ns | 96 B      |
| Record_Equals             |  3.1 ns | 0 B       |
| RecordStruct_Equals       |  2.8 ns | 0 B       |
| ZeroAlloc_Equals          |  3.1 ns | 0 B       |
| CFE_GetHashCode           | 38.7 ns | 88 B      |
| Record_GetHashCode        |  2.4 ns | 0 B       |
| RecordStruct_GetHashCode  |  2.2 ns | 0 B       |
| ZeroAlloc_GetHashCode     |  2.4 ns | 0 B       |

`record` and `ZeroAlloc.ValueObjects` are equivalent in performance — both use direct member comparison with no allocation. The difference is design freedom: records force the `record` keyword, add `EqualityContract`/`with`/deconstruct members, and cannot inherit from non-record classes. `ZeroAlloc.ValueObjects` works on any `partial class` or `partial struct` you already have.

Run your own benchmarks:

```
dotnet run -c Release --project benchmarks/ZeroAlloc.ValueObjects.Benchmarks
```

## Attributes

| Attribute | Target | Purpose |
|-----------|--------|---------|
| `[ValueObject]` | `partial class` or `partial struct` | Triggers generation |
| `[ValueObject(ForceClass = true)]` | `partial struct` | Override auto struct decision |
| `[EqualityMember]` | Property | Opt-in mode: only marked props participate |
| `[IgnoreEqualityMember]` | Property | Opt-out mode: exclude this prop |

### Default member selection

All `public` properties with a getter participate by default. If any property is marked `[EqualityMember]`, the mode switches to opt-in and only marked properties are included.

### Struct generation

Annotate your type as `partial struct` to emit a `readonly partial struct` instead of a `sealed partial class`.

```csharp
[ValueObject]
public partial struct CustomerId
{
    public Guid Value { get; }
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
