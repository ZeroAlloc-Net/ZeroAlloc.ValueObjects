using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace ZeroAlloc.ValueObjects;

/// <summary>
/// Hosted service that resolves the configured Snowflake worker-id factory at
/// startup, validates the result, and publishes it to
/// <see cref="TypedIdRuntime.SnowflakeProvider"/>.
/// </summary>
internal sealed class SnowflakeWorkerIdStartup : IHostedService
{
    private readonly Func<IServiceProvider, int> _factory;
    private readonly IServiceProvider _sp;

    public SnowflakeWorkerIdStartup(Func<IServiceProvider, int> factory, IServiceProvider sp)
    {
        _factory = factory;
        _sp = sp;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var id = _factory(_sp);
        if (id < 0 || id > SnowflakeCore.MaxWorkerId)
            throw new TypedIdException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Snowflake worker id {0} is out of range [0, {1}]. " +
                    "Call services.AddSnowflakeWorkerId with a valid id, set the configured env var, " +
                    "or register a factory that returns a valid value.",
                    id,
                    SnowflakeCore.MaxWorkerId));

        TypedIdRuntime.SnowflakeProvider = new StaticProvider(id);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private sealed class StaticProvider : ISnowflakeWorkerIdProvider
    {
        public StaticProvider(int workerId) => WorkerId = workerId;
        public int WorkerId { get; }
    }
}
