---
id: examples-finance
title: Finance Examples
slug: /docs/examples/finance
description: IBAN, Currency, and Amount value objects for financial domains.
sidebar_position: 17
---

# Finance Domain Examples

## `Iban` — validated, normalized value

```csharp
using ZeroAlloc.ValueObjects;

[ValueObject]
public partial class Iban
{
    public string Value { get; }

    public Iban(string raw)
    {
        var normalized = raw.Replace(" ", "").ToUpperInvariant();
        if (!IsValid(normalized))
            throw new ArgumentException($"'{raw}' is not a valid IBAN", nameof(raw));
        Value = normalized;
    }

    private static bool IsValid(string iban) =>
        iban.Length >= 15 && iban.Length <= 34 &&
        char.IsLetter(iban[0]) && char.IsLetter(iban[1]) &&
        char.IsDigit(iban[2]) && char.IsDigit(iban[3]);
}
```

```csharp
var iban1 = new Iban("DE89 3704 0044 0532 0130 00");
var iban2 = new Iban("DE89370400440532013000");  // same, different formatting

iban1 == iban2  // true — both normalize to "DE89370400440532013000"
```

---

## `CurrencyPair` — composite key for exchange rates

```csharp
[ValueObject]
public partial class CurrencyPair
{
    public string Base { get; }
    public string Quote { get; }

    public CurrencyPair(string @base, string quote)
    {
        Base = @base.ToUpperInvariant();
        Quote = quote.ToUpperInvariant();
    }

    public override string ToString() => $"{Base}/{Quote}";
}
```

```csharp
// Rate cache keyed by currency pair — no allocations on lookup
var rates = new Dictionary<CurrencyPair, decimal>
{
    [new CurrencyPair("USD", "EUR")] = 0.92m,
    [new CurrencyPair("USD", "GBP")] = 0.79m,
    [new CurrencyPair("EUR", "GBP")] = 0.86m,
};

var pair = new CurrencyPair("usd", "eur");  // lowercase — normalized in ctor
decimal rate = rates[pair];                 // 0.92m
```

---

## `AccountNumber` — strongly typed identifier (struct)

```csharp
[ValueObject]
public readonly partial struct AccountNumber
{
    public string Value { get; }

    public AccountNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Account number required");
        Value = value.Trim();
    }
}
```

```csharp
var acc1 = new AccountNumber("NL91ABNA0417164300");
var acc2 = new AccountNumber("NL91ABNA0417164300");

acc1 == acc2   // true — safe as dict key
```

---

## `Money` with ordering (manual `IComparable`)

The generator handles equality. Add ordering manually when needed:

```csharp
[ValueObject]
public partial class Money : IComparable<Money>
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency.ToUpperInvariant();
    }

    public int CompareTo(Money? other)
    {
        if (other is null) return 1;
        if (Currency != other.Currency)
            throw new InvalidOperationException($"Cannot compare {Currency} and {other.Currency}");
        return Amount.CompareTo(other.Amount);
    }

    public static bool operator <(Money  l, Money r) => l.CompareTo(r) < 0;
    public static bool operator >(Money  l, Money r) => l.CompareTo(r) > 0;
    public static bool operator <=(Money l, Money r) => l.CompareTo(r) <= 0;
    public static bool operator >=(Money l, Money r) => l.CompareTo(r) >= 0;
}
```

```csharp
var prices = new[]
{
    new Money(14.99m, "USD"),
    new Money(9.99m,  "USD"),
    new Money(24.99m, "USD"),
};

var sorted = prices.Order().ToArray();
// 9.99, 14.99, 24.99
```
