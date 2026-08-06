using System;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;

namespace ZeroAlloc.ValueObjects;

/// <summary>
/// Extension methods on <see cref="IServiceCollection"/> for registering the Snowflake
/// worker-id provider. The id is validated and published to
/// <see cref="TypedIdRuntime.SnowflakeProvider"/> immediately, so it is available to generated
/// <c>New()</c> methods without the host having to start.
/// <para>
/// These overloads resolve the worker id without consulting the service provider. To derive it
/// from a registered service, reference <c>ZeroAlloc.ValueObjects.Hosting</c>, which adds an
/// <c>AddSnowflakeWorkerId(Func&lt;IServiceProvider, int&gt;)</c> overload backed by a hosted
/// service. That package exists so this one does not depend on
/// <c>Microsoft.Extensions.Hosting.Abstractions</c>.
/// </para>
/// </summary>
public static class SnowflakeServiceCollectionExtensions
{
    /// <summary>Registers a fixed Snowflake worker id. Validated immediately.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="workerId">A worker id in the range <c>[0, <see cref="SnowflakeCore.MaxWorkerId"/>]</c>.</param>
    /// <returns>The same service collection, so calls can be chained.</returns>
    /// <exception cref="TypedIdException">The worker id is outside the valid range.</exception>
    public static IServiceCollection AddSnowflakeWorkerId(this IServiceCollection services, int workerId)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));

        SnowflakeWorkerIdPublisher.Publish(workerId);
        return services;
    }

    /// <summary>Reads the worker id from an environment variable, with a fallback. Validated immediately.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="envVar">Name of the environment variable to read.</param>
    /// <param name="fallback">Value used when the environment variable is missing or not a valid integer.</param>
    /// <returns>The same service collection, so calls can be chained.</returns>
    /// <exception cref="TypedIdException">The resolved worker id is outside the valid range.</exception>
    public static IServiceCollection AddSnowflakeWorkerId(this IServiceCollection services, string envVar, int fallback = 0)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));
        if (envVar is null) throw new ArgumentNullException(nameof(envVar));

        var raw = Environment.GetEnvironmentVariable(envVar);
        var workerId = int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;

        SnowflakeWorkerIdPublisher.Publish(workerId);
        return services;
    }

    /// <summary>Resolves the worker id via a factory. Invoked and validated immediately.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="factory">Factory invoked once to produce the worker id.</param>
    /// <returns>The same service collection, so calls can be chained.</returns>
    /// <exception cref="TypedIdException">The produced worker id is outside the valid range.</exception>
    public static IServiceCollection AddSnowflakeWorkerId(this IServiceCollection services, Func<int> factory)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));
        if (factory is null) throw new ArgumentNullException(nameof(factory));

        SnowflakeWorkerIdPublisher.Publish(factory());
        return services;
    }
}
