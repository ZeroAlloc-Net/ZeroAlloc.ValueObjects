using System;
using System.Threading;

namespace ZeroAlloc.ValueObjects;

/// <summary>
/// Low-level, allocation-free Snowflake id generation. Produces a positive 63-bit
/// <see cref="long"/> packed as <c>[1 reserved sign bit][41 ms since epoch][10 worker id][12 sequence]</c>.
/// </summary>
/// <remarks>
/// <para>
/// The epoch is fixed at <c>2020-01-01T00:00:00Z</c>, giving roughly 69 years of
/// timestamp range before the 41-bit field overflows.
/// </para>
/// <para>
/// Generation is lock-free: a packed <c>(lastMs, lastSeq)</c> tuple is updated via
/// <see cref="Interlocked.CompareExchange(ref long,long,long)"/>. If the per-millisecond
/// 12-bit sequence is exhausted, the call spin-waits until the next millisecond.
/// </para>
/// <para>
/// Clock rollback is handled defensively by continuing to increment the sequence at
/// <c>lastMs</c> rather than throwing, so transient NTP adjustments do not break
/// production id generation. When that also exhausts the sequence, the call spin-waits
/// for the clock to surpass <c>lastMs</c>. The wait is bounded by
/// <see cref="MaxSpinWaitMs"/> (default 5 s); exceeding the bound throws
/// <see cref="TypedIdException"/>.
/// </para>
/// </remarks>
public static class SnowflakeCore
{
    /// <summary>Fixed Snowflake epoch: <c>2020-01-01T00:00:00Z</c> in Unix milliseconds.</summary>
    private const long Epoch = 1577836800000L;

    private const int WorkerBits = 10;
    private const int SequenceBits = 12;

    /// <summary>Maximum permitted worker id (inclusive): <c>1023</c>.</summary>
    public const int MaxWorkerId = (1 << WorkerBits) - 1;

    private const long MaxSequence = (1L << SequenceBits) - 1;
    private const int TimestampShift = WorkerBits + SequenceBits;
    private const int WorkerShift = SequenceBits;

    // Packed state: top 41+ bits = lastMs (relative to epoch), bottom 12 bits = lastSeq.
    // Updated atomically via Interlocked.CompareExchange.
    private static long _state;

    /// <summary>
    /// Bound on the spin-wait performed when the clock is pinned to a prior millisecond
    /// (sequence exhaustion or severe rollback). Exceeding this throws
    /// <see cref="TypedIdException"/> rather than spinning forever. Exposed as
    /// <c>internal</c> so tests can shorten it.
    /// </summary>
    internal static int MaxSpinWaitMs { get; set; } = 5000;

    /// <summary>
    /// Returns the next Snowflake id for the given <paramref name="workerId"/>.
    /// </summary>
    /// <param name="workerId">Worker id in the range <c>[0, <see cref="MaxWorkerId"/>]</c>.</param>
    /// <returns>A strictly positive <see cref="long"/> id.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="workerId"/> is outside the permitted range.
    /// </exception>
    /// <exception cref="OverflowException">
    /// Thrown when the current system clock is earlier than the Snowflake epoch.
    /// </exception>
    public static long Next(int workerId)
    {
        if (workerId < 0 || workerId > MaxWorkerId)
            throw new ArgumentOutOfRangeException(nameof(workerId),
                $"Snowflake workerId must be in [0, {MaxWorkerId}].");

        while (true)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - Epoch;
            if (now < 0)
                throw new OverflowException("System clock is before the Snowflake epoch (2020-01-01T00:00:00Z).");

            long oldState = Interlocked.Read(ref _state);
            long lastMs = oldState >> SequenceBits;
            long lastSeq = oldState & MaxSequence;

            long newMs, newSeq;
            if (now > lastMs)
            {
                newMs = now;
                newSeq = 0;
            }
            else
            {
                // now == lastMs: same millisecond, bump sequence.
                // now <  lastMs: clock went backwards. Stay at lastMs and continue incrementing
                //                so ids remain monotonic across transient NTP adjustments.
                if (lastSeq >= MaxSequence)
                {
                    // Sequence exhausted in this ms — busy-wait for next ms, but bounded.
                    long waitUntil = lastMs;
                    int startTicks = Environment.TickCount;
                    int budget = MaxSpinWaitMs;
                    var spinner = default(SpinWait);
                    while (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - Epoch <= waitUntil)
                    {
                        if (Environment.TickCount - startTicks > budget)
                            throw new TypedIdException(
                                $"Snowflake generation stalled for {budget}ms waiting for the " +
                                "clock to advance past a previous id's millisecond. Check for " +
                                "severe clock skew.");
                        spinner.SpinOnce();
                    }
                    continue;
                }
                newMs = lastMs;
                newSeq = lastSeq + 1;
            }

            long newState = (newMs << SequenceBits) | newSeq;
            if (Interlocked.CompareExchange(ref _state, newState, oldState) == oldState)
            {
                // Compose the final id: [41 ms][10 worker][12 seq].
                return (newMs << TimestampShift) | ((long)workerId << WorkerShift) | newSeq;
            }
            // CAS failed — another thread advanced the state; retry.
        }
    }

    /// <summary>
    /// Extracts the 10-bit worker id from an id produced by <see cref="Next(int)"/>.
    /// Intended as a test / diagnostic helper.
    /// </summary>
    /// <param name="id">A Snowflake id.</param>
    /// <returns>The worker id in the range <c>[0, <see cref="MaxWorkerId"/>]</c>.</returns>
    public static int ExtractWorkerId(long id)
    {
        return (int)((id >> WorkerShift) & MaxWorkerId);
    }
}
