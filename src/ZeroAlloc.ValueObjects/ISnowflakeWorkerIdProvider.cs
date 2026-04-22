namespace ZeroAlloc.ValueObjects;

/// <summary>
/// Supplies the worker-id component for <see cref="IdStrategy.Snowflake"/> id generation.
/// Populated by <c>AddSnowflakeWorkerId</c> at host startup and read by the generated
/// <c>New()</c> method on Snowflake-strategy TypedId structs.
/// </summary>
public interface ISnowflakeWorkerIdProvider
{
    /// <summary>Worker id in the valid Snowflake range [0, 1023].</summary>
    int WorkerId { get; }
}
