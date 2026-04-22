namespace ZeroAlloc.ValueObjects.Tests;

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
}
