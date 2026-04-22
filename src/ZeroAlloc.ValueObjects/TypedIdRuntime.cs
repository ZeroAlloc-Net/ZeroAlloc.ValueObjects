namespace ZeroAlloc.ValueObjects;

/// <summary>
/// Shared process-wide runtime state consulted by generated TypedId code. Set by DI
/// extensions; read by the generator-emitted <c>New()</c> methods for strategies that
/// need configuration (currently <see cref="IdStrategy.Snowflake"/>).
/// </summary>
public static class TypedIdRuntime
{
    /// <summary>
    /// Registered worker-id provider for <see cref="IdStrategy.Snowflake"/>. Null until
    /// <c>AddSnowflakeWorkerId</c> is called (and its hosted service has started). When
    /// null, generated Snowflake <c>New()</c> falls back to the <c>ZA_SNOWFLAKE_WORKER_ID</c>
    /// environment variable, then throws <see cref="TypedIdException"/>.
    /// </summary>
    public static ISnowflakeWorkerIdProvider? SnowflakeProvider { get; internal set; }
}
