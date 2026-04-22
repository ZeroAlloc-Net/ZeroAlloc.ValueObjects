namespace ZeroAlloc.ValueObjects.Tests;

public sealed class SequentialCoreTests
{
    [Fact]
    public void Next_IsStrictlyIncreasing()
    {
        SequentialCore.Reset();
        Assert.Equal(1, SequentialCore.Next());
        Assert.Equal(2, SequentialCore.Next());
        Assert.Equal(3, SequentialCore.Next());
    }

    [Fact]
    public async Task Next_16ConcurrentCallers_AllUnique()
    {
        SequentialCore.Reset();
        const int perTask = 10_000;
        var tasks = Enumerable.Range(0, 16).Select(_ => Task.Run(() =>
        {
            var local = new HashSet<long>();
            for (int i = 0; i < perTask; i++) local.Add(SequentialCore.Next());
            return local;
        })).ToArray();
        await Task.WhenAll(tasks);
        var all = tasks.SelectMany(t => t.Result).ToHashSet();
        Assert.Equal(16 * perTask, all.Count);
    }

    [Fact]
    public void Reset_RestartsCounterFrom1()
    {
        SequentialCore.Next();
        SequentialCore.Next();
        SequentialCore.Reset();
        Assert.Equal(1, SequentialCore.Next());
    }
}
