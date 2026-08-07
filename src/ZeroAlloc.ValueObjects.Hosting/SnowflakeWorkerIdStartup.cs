using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace ZeroAlloc.ValueObjects;

/// <summary>
/// Hosted service that resolves the configured Snowflake worker-id factory at
/// startup, validates the result, and publishes it to
/// <see cref="TypedIdRuntime.SnowflakeProvider"/>.
/// </summary>
/// <remarks>
/// Only the <c>Func&lt;IServiceProvider, int&gt;</c> overload needs this: it is the one form that
/// cannot be resolved until the container is built. The overloads that take a literal, an
/// environment variable name, or a <c>Func&lt;int&gt;</c> live in ZeroAlloc.ValueObjects and
/// publish eagerly, which is why that package needs no hosting dependency.
/// </remarks>
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
        // Validation and publication live in ZeroAlloc.ValueObjects so this path and the eager
        // ones cannot drift — an out-of-range id throws the same TypedIdException either way.
        SnowflakeWorkerIdPublisher.Publish(_factory(_sp));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
