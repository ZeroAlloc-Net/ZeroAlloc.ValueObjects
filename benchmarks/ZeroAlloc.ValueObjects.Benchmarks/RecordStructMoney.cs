namespace ZeroAlloc.ValueObjects.Benchmarks;

// C# readonly record struct — value-type record, stack allocated, no hidden copies
public readonly record struct RecordStructMoney(decimal Amount, string Currency);
