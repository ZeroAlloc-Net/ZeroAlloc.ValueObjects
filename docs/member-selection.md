# Member Selection Rules

The generator inspects all **public instance properties with a getter** on the type.

## Decision flow

```mermaid
flowchart TD
    Start([Public property with getter]) --> Q1{Any property\nin type has\n[EqualityMember]?}
    Q1 -->|Yes – opt-in mode| Q2{This property\nhas [EqualityMember]?}
    Q2 -->|Yes| Include([Included in equality])
    Q2 -->|No| Exclude([Excluded from equality])
    Q1 -->|No – default / opt-out mode| Q3{This property\nhas [IgnoreEqualityMember]?}
    Q3 -->|Yes| Exclude
    Q3 -->|No| Include
```

## Default mode — no attributes

All public properties with getters are included.

```csharp
[ValueObject]
public partial class Invoice
{
    public string Number { get; }    // included
    public decimal Total { get; }    // included
    public string Status { get; }    // included
}
```

## Opt-out mode — `[IgnoreEqualityMember]`

All public properties are included **except** those marked `[IgnoreEqualityMember]`.

Use this when you want most properties to participate and only need to exclude a few tracking/audit fields.

```csharp
[ValueObject]
public partial class Invoice
{
    public string Number { get; }                              // included
    public decimal Total { get; }                              // included
    [IgnoreEqualityMember] public DateTime PrintedAt { get; } // excluded
    [IgnoreEqualityMember] public Guid AuditTrailId { get; }  // excluded
}
```

## Opt-in mode — `[EqualityMember]`

When **any** property in the type carries `[EqualityMember]`, the generator switches to opt-in mode. **Only** marked properties participate.

Use this when only a small subset of properties defines identity and you want to be explicit.

```csharp
[ValueObject]
public partial class Invoice
{
    [EqualityMember] public string Number { get; }   // included
    public decimal Total { get; }                    // excluded (unmarked in opt-in mode)
    public DateTime PrintedAt { get; }               // excluded
}
```

## What is excluded regardless of mode

- Non-public properties (`private`, `internal`, `protected`)
- Static properties
- Write-only properties (no getter)

These are never included, even in default mode.
