using System.Threading;

namespace ZeroAlloc.ValueObjects;

/// <summary>
/// Monotonic <see cref="long"/> counter for <see cref="IdStrategy.Sequential"/> id generation.
/// Not persisted across process restarts — intended for test stability and simple local
/// ordering, not cross-process uniqueness.
/// </summary>
public static class SequentialCore
{
    private static long _counter;

    /// <summary>
    /// Returns the next sequential value (1-based). Lock-free via
    /// <see cref="Interlocked.Increment(ref long)"/>.
    /// </summary>
    public static long Next() => Interlocked.Increment(ref _counter);

    /// <summary>
    /// Test-only: resets the counter so the next <see cref="Next"/> call returns <c>1</c>.
    /// </summary>
    public static void Reset() => Interlocked.Exchange(ref _counter, 0);
}
