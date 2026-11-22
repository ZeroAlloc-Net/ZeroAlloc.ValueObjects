using ZeroAlloc.ValueObjects;
using ZeroAlloc.ValueObjects.Sdk10Smoke;

// OrderId.New and OrderId.Parse exist only if the compiler loaded the bundled generator.
var id = OrderId.New();
var parsed = OrderId.Parse(id.ToString(), null);
if (parsed != id)
{
    Console.Error.WriteLine($"FAIL: {id} parsed back as {parsed}");
    return 1;
}

// Without the generator, Money keeps reference equality.
if (!new Money("EUR", 10m).Equals(new Money("EUR", 10m)))
{
    Console.Error.WriteLine("FAIL: Money has no generated value equality");
    return 1;
}

Console.WriteLine("SDK 10 consumer: generator loaded, typed ID and value equality generated.");
return 0;

namespace ZeroAlloc.ValueObjects.Sdk10Smoke
{
    [TypedId(Strategy = IdStrategy.Ulid)]
    public readonly partial record struct OrderId;

    [ValueObject]
    public partial class Money
    {
        public Money(string currency, decimal amount) => (Currency, Amount) = (currency, amount);

        public string Currency { get; }

        public decimal Amount { get; }
    }
}
