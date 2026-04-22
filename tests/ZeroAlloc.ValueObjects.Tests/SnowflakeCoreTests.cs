using System.Reflection;

namespace ZeroAlloc.ValueObjects.Tests;

// Tests that mutate SnowflakeCore._state or MaxSpinWaitMs must not run in parallel
// with other tests touching SnowflakeCore; those tests are serialized via a
// shared xUnit collection.
[Collection("SnowflakeCoreState")]
public sealed class SnowflakeCoreTests
{
    [Fact]
    public void Next_WithWorkerId_PacksWorkerIntoBits()
    {
        var id = SnowflakeCore.Next(workerId: 5);
        Assert.Equal(5, SnowflakeCore.ExtractWorkerId(id));
    }

    [Fact]
    public void Next_MultipleCalls_AreStrictlyIncreasing()
    {
        long prev = SnowflakeCore.Next(1);
        for (int i = 0; i < 1000; i++)
        {
            long next = SnowflakeCore.Next(1);
            Assert.True(next > prev, $"Not increasing at i={i}: {prev} -> {next}");
            prev = next;
        }
    }

    [Fact]
    public void Next_WithNegativeWorkerId_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => SnowflakeCore.Next(workerId: -1));

    [Fact]
    public void Next_WithWorkerIdOutOfRange_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => SnowflakeCore.Next(workerId: 1024));

    [Fact]
    public void Next_IdIsPositive()
    {
        for (int i = 0; i < 100; i++) Assert.True(SnowflakeCore.Next(0) > 0);
    }

    [Fact]
    public void Next_SpinWaitExceedsBudget_ThrowsTypedIdException()
    {
        // Simulate the sequence-exhaustion branch by seeding _state with a far-future
        // timestamp and a full sequence counter, so every call takes the bounded-spin
        // branch and exceeds a tiny budget.
        var stateField = typeof(SnowflakeCore)
            .GetField("_state", BindingFlags.NonPublic | BindingFlags.Static)!;
        var originalState = (long)stateField.GetValue(null)!;
        var originalBudget = SnowflakeCore.MaxSpinWaitMs;
        try
        {
            // lastMs one hour in the future (relative to epoch), lastSeq = MaxSequence (4095).
            long futureMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                            - 1577836800000L + 3_600_000L;
            long packed = (futureMs << 12) | 4095L;
            stateField.SetValue(null, packed);
            SnowflakeCore.MaxSpinWaitMs = 25;

            var ex = Assert.Throws<TypedIdException>(() => SnowflakeCore.Next(1));
            Assert.Contains("stalled", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            SnowflakeCore.MaxSpinWaitMs = originalBudget;
            stateField.SetValue(null, originalState);
        }
    }
}
