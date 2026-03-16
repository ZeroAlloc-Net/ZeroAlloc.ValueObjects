# HR / Identity Domain Examples

## `EmailAddress` — single-property, normalized

```csharp
using ZeroAlloc.ValueObjects;

[ValueObject]
public partial class EmailAddress
{
    public string Value { get; }

    public EmailAddress(string value)
    {
        if (!value.Contains('@'))
            throw new ArgumentException("Invalid email address", nameof(value));
        Value = value.Trim().ToLowerInvariant();
    }

    public string Domain => Value.Split('@')[1];
}
```

```csharp
var e1 = new EmailAddress("Alice@Example.com");
var e2 = new EmailAddress("alice@example.com");  // same after normalization

e1 == e2   // true

// Deduplicating an invite list
var invites = new HashSet<EmailAddress>
{
    new("alice@example.com"),
    new("Alice@Example.com"),  // duplicate
    new("bob@example.com"),
};
// invites.Count == 2
```

---

## `EmployeeId` — typed wrapper (struct)

```csharp
[ValueObject]
public readonly partial struct EmployeeId
{
    public int Value { get; }

    public EmployeeId(int value)
    {
        if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
        Value = value;
    }

    public static EmployeeId Parse(string s) => new(int.Parse(s));
}
```

```csharp
// Prevent confusing employee IDs with department IDs at compile time
void AssignToDepartment(EmployeeId employee, DepartmentId department) { ... }

AssignToDepartment(new EmployeeId(42), new DepartmentId(7));   // OK
AssignToDepartment(new DepartmentId(7), new EmployeeId(42));   // compile error
```

---

## `FullName` — multi-component, nullable middle name

```csharp
[ValueObject]
public partial class FullName
{
    public string First { get; }
    public string Last { get; }
    public string? Middle { get; }

    public FullName(string first, string last, string? middle = null)
    {
        First = first.Trim();
        Last = last.Trim();
        Middle = middle?.Trim();
    }

    public string Display => Middle is null
        ? $"{First} {Last}"
        : $"{First} {Middle[0]}. {Last}";
}
```

```csharp
var name1 = new FullName("John", "Smith");
var name2 = new FullName("John", "Smith", null);
var name3 = new FullName("John", "Smith", "Robert");

name1 == name2  // true  — Middle is null in both
name1 == name3  // false — Middle differs (null vs "Robert")
```

---

## `Department` — value object with code + label

```csharp
[ValueObject]
public readonly partial struct DepartmentId
{
    public int Value { get; }
    public DepartmentId(int value) => Value = value;
}

[ValueObject]
public partial class Department
{
    [EqualityMember] public DepartmentId Id { get; }

    // Label is cosmetic — not part of identity
    public string Name { get; }

    public Department(DepartmentId id, string name)
    {
        Id = id; Name = name;
    }
}
```

```csharp
var dept1 = new Department(new DepartmentId(10), "Engineering");
var dept2 = new Department(new DepartmentId(10), "Eng");  // renamed

dept1 == dept2  // true — same Id regardless of Name
```
