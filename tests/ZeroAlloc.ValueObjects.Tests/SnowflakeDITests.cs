using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ZeroAlloc.ValueObjects.Tests;

public sealed class SnowflakeDITests : IDisposable
{
    // Tests mutate the static TypedIdRuntime.SnowflakeProvider — ensure xUnit test collection
    // disables parallelization for this class. xUnit2 runs tests in a class sequentially by default,
    // so using a single class is sufficient.
    private readonly ISnowflakeWorkerIdProvider? _originalProvider;

    public SnowflakeDITests()
    {
        _originalProvider = TypedIdRuntime.SnowflakeProvider;
    }

    public void Dispose()
    {
        TypedIdRuntime.SnowflakeProvider = _originalProvider;
    }

    [Fact]
    public async Task AddSnowflakeWorkerId_Literal_PopulatesRuntimeProviderOnHostStart()
    {
        TypedIdRuntime.SnowflakeProvider = null;
        using var host = new HostBuilder()
            .ConfigureServices(s => s.AddSnowflakeWorkerId(workerId: 42))
            .Build();
        await host.StartAsync();
        Assert.NotNull(TypedIdRuntime.SnowflakeProvider);
        Assert.Equal(42, TypedIdRuntime.SnowflakeProvider!.WorkerId);
        await host.StopAsync();
    }

    [Fact]
    public async Task AddSnowflakeWorkerId_EnvVar_ReadsFromEnvironment()
    {
        TypedIdRuntime.SnowflakeProvider = null;
        Environment.SetEnvironmentVariable("ZA_TEST_WORKER", "7");
        try
        {
            using var host = new HostBuilder()
                .ConfigureServices(s => s.AddSnowflakeWorkerId(envVar: "ZA_TEST_WORKER"))
                .Build();
            await host.StartAsync();
            Assert.Equal(7, TypedIdRuntime.SnowflakeProvider!.WorkerId);
            await host.StopAsync();
        }
        finally { Environment.SetEnvironmentVariable("ZA_TEST_WORKER", null); }
    }

    [Fact]
    public async Task AddSnowflakeWorkerId_Factory_InvokesFactoryOnStart()
    {
        TypedIdRuntime.SnowflakeProvider = null;
        int called = 0;
        using var host = new HostBuilder()
            .ConfigureServices(s => s.AddSnowflakeWorkerId(_ => { called++; return 99; }))
            .Build();
        await host.StartAsync();
        Assert.Equal(1, called);
        Assert.Equal(99, TypedIdRuntime.SnowflakeProvider!.WorkerId);
        await host.StopAsync();
    }

    [Fact]
    public async Task AddSnowflakeWorkerId_OutOfRangeId_ThrowsAtStart()
    {
        using var host = new HostBuilder()
            .ConfigureServices(s => s.AddSnowflakeWorkerId(workerId: 2048))
            .Build();
        await Assert.ThrowsAsync<TypedIdException>(async () => await host.StartAsync().ConfigureAwait(false));
    }

    [Fact]
    public async Task AddSnowflakeWorkerId_EnvVar_MissingValue_UsesFallback()
    {
        TypedIdRuntime.SnowflakeProvider = null;
        Environment.SetEnvironmentVariable("ZA_MISSING_WORKER", null);
        using var host = new HostBuilder()
            .ConfigureServices(s => s.AddSnowflakeWorkerId(envVar: "ZA_MISSING_WORKER", fallback: 3))
            .Build();
        await host.StartAsync();
        Assert.Equal(3, TypedIdRuntime.SnowflakeProvider!.WorkerId);
        await host.StopAsync();
    }
}
