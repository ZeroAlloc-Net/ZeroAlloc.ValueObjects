---
id: nullable-properties
title: Nullable Properties
slug: /docs/nullable-properties
description: Null-safe equality comparison for nullable reference type members.
sidebar_position: 8
---

# Nullable Properties

Properties declared with a nullable reference type (`string?`, `MyClass?`) receive null-safe comparison in the generated `Equals` method.

## Example

```csharp
[ValueObject]
public partial class Contact
{
    public string Name { get; }
    public string? Email { get; }    // nullable
    public string? Phone { get; }    // nullable

    public Contact(string name, string? email = null, string? phone = null)
    {
        Name = name; Email = email; Phone = phone;
    }
}
```

Generated equality:

```csharp
public bool Equals(Contact? other) =>
    other is not null &&
    Name == other.Name &&
    (other.Email is null ? Email is null : Email == other.Email) &&
    (other.Phone is null ? Phone is null : Phone == other.Phone);
```

The null-safe pattern avoids a `NullReferenceException` without any extra heap allocation.

## Behaviour

```csharp
var c1 = new Contact("Alice", null, "555-0100");
var c2 = new Contact("Alice", null, "555-0100");
var c3 = new Contact("Alice", "alice@example.com", "555-0100");

c1 == c2  // true  — both Email are null
c1 == c3  // false — Email differs (null vs non-null)
```

## Non-nullable properties

For non-nullable properties (`string`, `MyClass`), a simple `==` comparison is generated. The compiler's null-safety analysis ensures these are never `null` at runtime when nullable reference types are enabled.

```csharp
// Non-nullable → simple comparison
Name == other.Name

// Nullable → null-safe comparison
(other.Email is null ? Email is null : Email == other.Email)
```
