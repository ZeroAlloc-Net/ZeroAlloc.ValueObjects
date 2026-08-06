using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ZeroAlloc.ValueObjects;

/// <summary>
/// Adds the host-integrated <c>AddSnowflakeWorkerId</c> overload — the one that derives the worker
/// id from the built service provider.
/// <para>
/// The namespace matches ZeroAlloc.ValueObjects deliberately: an existing
/// <c>using ZeroAlloc.ValueObjects;</c> keeps compiling once this package is referenced, so
/// adopting it is a package reference and nothing more.
/// </para>
/// </summary>
public static class SnowflakeHostingServiceCollectionExtensions
{
    /// <summary>
    /// Resolves the worker id via a factory that receives the application's
    /// <see cref="IServiceProvider"/>. The factory runs once at host startup, after the container
    /// is built, and the result is validated then.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="factory">Factory invoked once during startup to produce the worker id.</param>
    /// <returns>The same service collection, so calls can be chained.</returns>
    /// <remarks>
    /// Requires a host that runs <c>IHostedService</c> registrations. If the worker id does not
    /// depend on a registered service, prefer the overloads in ZeroAlloc.ValueObjects: they publish
    /// immediately and do not require the host to start.
    /// </remarks>
    public static IServiceCollection AddSnowflakeWorkerId(this IServiceCollection services, Func<IServiceProvider, int> factory)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));
        if (factory is null) throw new ArgumentNullException(nameof(factory));

        services.TryAddSingleton(factory);
        services.AddHostedService<SnowflakeWorkerIdStartup>();
        return services;
    }
}
