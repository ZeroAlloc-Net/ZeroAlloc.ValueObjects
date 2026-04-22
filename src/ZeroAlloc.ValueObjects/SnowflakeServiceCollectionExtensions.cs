using System;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ZeroAlloc.ValueObjects;

/// <summary>
/// Extension methods on <see cref="IServiceCollection"/> for registering the Snowflake
/// worker-id provider. The id is validated and published to
/// <see cref="TypedIdRuntime.SnowflakeProvider"/> by a hosted service at host startup.
/// </summary>
public static class SnowflakeServiceCollectionExtensions
{
    /// <summary>Registers a fixed Snowflake worker id. Validated at host startup.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="workerId">A worker id in the range <c>[0, <see cref="SnowflakeCore.MaxWorkerId"/>]</c>.</param>
    public static IServiceCollection AddSnowflakeWorkerId(this IServiceCollection services, int workerId)
        => AddSnowflakeWorkerId(services, sp => workerId);

    /// <summary>Reads the worker id from an environment variable at host startup, with a fallback.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="envVar">Name of the environment variable to read.</param>
    /// <param name="fallback">Value used when the environment variable is missing or not a valid integer.</param>
    public static IServiceCollection AddSnowflakeWorkerId(this IServiceCollection services, string envVar, int fallback = 0)
    {
        if (envVar is null) throw new ArgumentNullException(nameof(envVar));
        return AddSnowflakeWorkerId(services, sp =>
        {
            var raw = Environment.GetEnvironmentVariable(envVar);
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;
        });
    }

    /// <summary>Resolves the worker id via a factory at host startup.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="factory">Factory invoked once during startup to produce the worker id.</param>
    public static IServiceCollection AddSnowflakeWorkerId(this IServiceCollection services, Func<IServiceProvider, int> factory)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));
        if (factory is null) throw new ArgumentNullException(nameof(factory));

        services.TryAddSingleton(factory);
        services.AddHostedService<SnowflakeWorkerIdStartup>();
        return services;
    }
}
