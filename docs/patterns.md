# Usage Patterns

## Dictionary Keys

The most common motivation for correct `GetHashCode` + `Equals` is using a type as a dictionary key. The generator makes this safe and allocation-free.

```csharp
[ValueObject]
public partial class CacheKey
{
    public string Region { get; }
    public string Id { get; }
    public string Version { get; }

    public CacheKey(string region, string id, string version)
    {
        Region = region; Id = id; Version = version;
    }
}

// In a caching layer
var cache = new Dictionary<CacheKey, byte[]>();

void Store(string region, string id, string version, byte[] data)
    => cache[new CacheKey(region, id, version)] = data;

byte[]? Fetch(string region, string id, string version)
{
    var key = new CacheKey(region, id, version);
    return cache.TryGetValue(key, out var data) ? data : null;
}
```

Every dictionary lookup calls `GetHashCode()` once and `Equals()` on collision — both are zero-allocation.

---

## HashSets

Use `HashSet<T>` with value objects for deduplication or membership tests.

```csharp
[ValueObject]
public partial class Tag
{
    public string Value { get; }
    public Tag(string value) => Value = value.ToLowerInvariant().Trim();
}

// Deduplication
var rawTags = new[] { "C#", "c#", " C# ", "dotnet", "DotNet" };
var uniqueTags = rawTags.Select(t => new Tag(t)).ToHashSet();
// Result: { "c#", "dotnet" }

// Membership test
bool hasTag = uniqueTags.Contains(new Tag("C#"));  // true
```

---

## LINQ GroupBy

`GroupBy` uses a dictionary internally. Correct equality makes grouping by value objects work naturally.

```csharp
[ValueObject]
public readonly partial struct Department
{
    public string Code { get; }
    public string Name { get; }
    public Department(string code, string name) { Code = code; Name = name; }
}

var employees = GetEmployees();

var byDepartment = employees
    .GroupBy(e => e.Department)
    .ToDictionary(g => g.Key, g => g.ToList());
```

Employees with the same `Department` value object land in the same group.

---

## EF Core — Owned Entities

Value objects commonly map to EF Core **owned entities**. `[ValueObject]` handles equality at the domain layer; EF Core handles persistence.

```csharp
[ValueObject]
public partial class PostalAddress
{
    [EqualityMember] public string Line1 { get; }
    [EqualityMember] public string? Line2 { get; }
    [EqualityMember] public string City { get; }
    [EqualityMember] public string PostalCode { get; }
    [EqualityMember] public string Country { get; }

    // Audit field — not part of domain identity
    public DateTime UpdatedAt { get; private set; }

    public PostalAddress(string line1, string? line2, string city, string postalCode, string country)
    {
        Line1 = line1; Line2 = line2; City = city;
        PostalCode = postalCode; Country = country;
        UpdatedAt = DateTime.UtcNow;
    }
}

// EF Core mapping
public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.OwnsOne(o => o.ShippingAddress, addr =>
        {
            addr.Property(a => a.Line1).HasMaxLength(200);
            addr.Property(a => a.City).HasMaxLength(100);
            addr.Property(a => a.PostalCode).HasMaxLength(20);
            addr.Property(a => a.Country).HasMaxLength(2);
        });
    }
}
```

---

## EF Core — Value Converters

For single-value wrapper types (strongly typed IDs), use a value converter:

```csharp
[ValueObject]
public partial class EmailAddress
{
    public string Value { get; }
    public EmailAddress(string value) => Value = value.ToLowerInvariant().Trim();
}

// In DbContext or IEntityTypeConfiguration
modelBuilder.Entity<User>()
    .Property(u => u.Email)
    .HasConversion(
        e => e.Value,
        s => new EmailAddress(s));
```

---

## Pattern Matching

Value objects work naturally with C# switch expressions.

```csharp
[ValueObject]
public partial class OrderStatus
{
    public string Value { get; }
    public OrderStatus(string value) => Value = value;

    public static readonly OrderStatus Pending   = new("Pending");
    public static readonly OrderStatus Confirmed = new("Confirmed");
    public static readonly OrderStatus Shipped   = new("Shipped");
    public static readonly OrderStatus Cancelled = new("Cancelled");
}

string Describe(OrderStatus status) => status switch
{
    _ when status == OrderStatus.Pending   => "Awaiting confirmation",
    _ when status == OrderStatus.Confirmed => "Processing",
    _ when status == OrderStatus.Shipped   => "On its way",
    _ when status == OrderStatus.Cancelled => "Cancelled",
    _                                      => "Unknown"
};
```

---

## JSON Serialization (System.Text.Json)

For types with constructor parameters, register a custom converter:

```csharp
[ValueObject]
[JsonConverter(typeof(EmailAddressConverter))]
public partial class EmailAddress
{
    public string Value { get; }
    public EmailAddress(string value) => Value = value.ToLowerInvariant().Trim();
}

public class EmailAddressConverter : JsonConverter<EmailAddress>
{
    public override EmailAddress Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        => new(reader.GetString()!);

    public override void Write(Utf8JsonWriter writer, EmailAddress value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
```
