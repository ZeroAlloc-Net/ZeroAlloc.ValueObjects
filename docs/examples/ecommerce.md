# E-Commerce Domain Examples

## `ProductId` — strongly typed identifier (struct)

Using a struct wraps a primitive in a type-safe shell with zero overhead.

```csharp
using ZeroAlloc.ValueObjects;

[ValueObject]
public readonly partial struct ProductId
{
    public Guid Value { get; }

    public ProductId(Guid value) => Value = value;

    public static ProductId New() => new(Guid.NewGuid());
    public static ProductId Parse(string s) => new(Guid.Parse(s));

    public override string ToString() => Value.ToString();
}
```

```csharp
var id1 = ProductId.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
var id2 = ProductId.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");

id1 == id2        // true
id1.Equals(id2)   // true

var catalog = new Dictionary<ProductId, Product>();
catalog[id1] = product;
var found = catalog[id2];  // works — same hash, equal
```

---

## `Money` — value with currency

```csharp
[ValueObject]
public partial class Money
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency)
    {
        if (amount < 0) throw new ArgumentException("Amount cannot be negative");
        if (string.IsNullOrWhiteSpace(currency)) throw new ArgumentException("Currency required");
        Amount = amount;
        Currency = currency.ToUpperInvariant();
    }

    public Money Add(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException($"Cannot add {Currency} and {other.Currency}");
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Multiply(decimal factor) => new(Amount * factor, Currency);

    public static Money Zero(string currency) => new(0m, currency);
}
```

```csharp
var subtotal = new Money(49.99m, "USD");
var shipping = new Money(5.00m, "USD");
var total    = subtotal.Add(shipping);

// Deduplication in a set of prices
var uniquePrices = new HashSet<Money>
{
    new Money(9.99m, "USD"),
    new Money(9.99m, "USD"),  // duplicate, not added
    new Money(14.99m, "USD"),
};
// uniquePrices.Count == 2
```

---

## `ShippingAddress` — partial equality

Shipping addresses should be compared on delivery-relevant fields only.

```csharp
[ValueObject]
public partial class ShippingAddress
{
    [EqualityMember] public string Line1 { get; }
    [EqualityMember] public string? Line2 { get; }
    [EqualityMember] public string City { get; }
    [EqualityMember] public string State { get; }
    [EqualityMember] public string PostalCode { get; }
    [EqualityMember] public string Country { get; }

    // Cosmetic / operational — not part of domain identity
    public string? DeliveryInstructions { get; }
    public bool IsDefault { get; }

    public ShippingAddress(
        string line1, string? line2, string city,
        string state, string postalCode, string country,
        string? deliveryInstructions = null,
        bool isDefault = false)
    {
        Line1 = line1; Line2 = line2; City = city;
        State = state; PostalCode = postalCode; Country = country;
        DeliveryInstructions = deliveryInstructions;
        IsDefault = isDefault;
    }
}
```

```csharp
var addr1 = new ShippingAddress("123 Main St", null, "Springfield", "IL", "62701", "US",
    deliveryInstructions: "Ring bell");
var addr2 = new ShippingAddress("123 Main St", null, "Springfield", "IL", "62701", "US",
    deliveryInstructions: "Leave at door");

// Same delivery address, different instructions — equal for dedup purposes
addr1 == addr2   // true

var saved = new HashSet<ShippingAddress> { addr1, addr2 };
// saved.Count == 1
```

---

## `Discount` — opt-out with audit fields

```csharp
[ValueObject]
public partial class Discount
{
    public string Code { get; }
    public decimal Percentage { get; }
    public DateTime ValidFrom { get; }
    public DateTime ValidTo { get; }

    [IgnoreEqualityMember] public Guid IssuedBy { get; }
    [IgnoreEqualityMember] public DateTime CreatedAt { get; }

    public Discount(string code, decimal percentage, DateTime validFrom, DateTime validTo, Guid issuedBy)
    {
        Code = code; Percentage = percentage;
        ValidFrom = validFrom; ValidTo = validTo;
        IssuedBy = issuedBy; CreatedAt = DateTime.UtcNow;
    }

    public bool IsValid(DateTime at) => at >= ValidFrom && at <= ValidTo;
}
```

Two discounts with the same code, percentage, and validity window are equal — regardless of who issued them or when the record was created.
