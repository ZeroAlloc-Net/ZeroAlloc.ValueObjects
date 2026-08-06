using System.Globalization;

namespace ZeroAlloc.ValueObjects;

/// <summary>
/// Validates a Snowflake worker id and publishes it to <see cref="TypedIdRuntime.SnowflakeProvider"/>.
/// <para>
/// Shared by the registration overloads in this assembly, which resolve the id eagerly, and by the
/// host-integrated overload in ZeroAlloc.ValueObjects.Hosting, which resolves it from the built
/// service provider at startup. Keeping validation here means both paths produce the same
/// <see cref="TypedIdException"/> for an out-of-range id.
/// </para>
/// </summary>
internal static class SnowflakeWorkerIdPublisher
{
    internal static void Publish(int workerId)
    {
        if (workerId < 0 || workerId > SnowflakeCore.MaxWorkerId)
            throw new TypedIdException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Snowflake worker id {0} is out of range [0, {1}]. " +
                    "Call services.AddSnowflakeWorkerId with a valid id, set the configured env var, " +
                    "or register a factory that returns a valid value.",
                    workerId,
                    SnowflakeCore.MaxWorkerId));

        TypedIdRuntime.SnowflakeProvider = new StaticProvider(workerId);
    }

    private sealed class StaticProvider : ISnowflakeWorkerIdProvider
    {
        internal StaticProvider(int workerId) => WorkerId = workerId;

        public int WorkerId { get; }
    }
}
