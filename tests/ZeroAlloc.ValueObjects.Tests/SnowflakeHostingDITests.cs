using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ZeroAlloc.ValueObjects.Tests;

/// <summary>
/// Covers the host-integrated <c>AddSnowflakeWorkerId(Func&lt;IServiceProvider, int&gt;)</c> overload
/// that ships in ZeroAlloc.ValueObjects.Hosting.
/// <para>
/// This is the one overload that cannot resolve its id until the container is built, which is why
/// it kept the hosted service and moved to a separate package (ValueObjects#58). Its timing
/// contract is therefore the opposite of the eager overloads: nothing is published until the host
/// starts.
/// </para>
/// </summary>
[Collection("SnowflakeProviderMutation")]
public sealed class SnowflakeHostingDITests : IDisposable
{
    private readonly ISnowflakeWorkerIdProvider? _originalProvider;

    public SnowflakeHostingDITests()
    {
        _originalProvider = TypedIdRuntime.SnowflakeProvider;
    }

    public void Dispose()
    {
        TypedIdRuntime.SnowflakeProvider = _originalProvider;
    }

    [Fact]
    public async Task AddSnowflakeWorkerId_Factory_InvokesFactoryOnStart()
    {
        TypedIdRuntime.SnowflakeProvider = null;
        int called = 0;

        using var host = new HostBuilder()
            .ConfigureServices(s => s.AddSnowflakeWorkerId(_ => { called++; return 99; }))
            .Build();

        // Still deferred: building the container must not run the factory.
        Assert.Equal(0, called);
        Assert.Null(TypedIdRuntime.SnowflakeProvider);

        await host.StartAsync();

        Assert.Equal(1, called);
        Assert.Equal(99, TypedIdRuntime.SnowflakeProvider!.WorkerId);
        await host.StopAsync();
    }

    [Fact]
    public async Task AddSnowflakeWorkerId_Factory_ResolvesFromServiceProvider()
    {
        TypedIdRuntime.SnowflakeProvider = null;

        using var host = new HostBuilder()
            .ConfigureServices(s =>
            {
                s.AddSingleton(new MachineIdSource(17));
                s.AddSnowflakeWorkerId(sp => sp.GetRequiredService<MachineIdSource>().Id);
            })
            .Build();

        await host.StartAsync();

        Assert.Equal(17, TypedIdRuntime.SnowflakeProvider!.WorkerId);
        await host.StopAsync();
    }

    [Fact]
    public async Task AddSnowflakeWorkerId_OutOfRangeFromFactory_ThrowsAtStart()
    {
        // Validation is shared with the eager overloads, so the exception type matches even
        // though the timing differs.
        using var host = new HostBuilder()
            .ConfigureServices(s => s.AddSnowflakeWorkerId(_ => 2048))
            .Build();

        await Assert.ThrowsAsync<TypedIdException>(async () => await host.StartAsync().ConfigureAwait(false));
    }

    private sealed class MachineIdSource
    {
        public MachineIdSource(int id) => Id = id;

        public int Id { get; }
    }
}
