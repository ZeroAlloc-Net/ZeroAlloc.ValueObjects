using Microsoft.Extensions.DependencyInjection;

namespace ZeroAlloc.ValueObjects.Tests;

/// <summary>
/// Covers the <c>AddSnowflakeWorkerId</c> overloads that ship in ZeroAlloc.ValueObjects itself.
/// <para>
/// These resolve the worker id without the service provider, so they publish it immediately
/// rather than from a hosted service — which is what lets the core package drop its
/// Microsoft.Extensions.Hosting.Abstractions dependency (ValueObjects#58). None of these tests
/// builds a host; that is the point. The host-integrated overload is covered by
/// <see cref="SnowflakeHostingDITests"/>.
/// </para>
/// </summary>
// Snowflake id generation touches process-wide static state: the provider slot in
// TypedIdRuntime and SnowflakeCore's packed timestamp/sequence plus MaxSpinWaitMs.
// Every class touching either shares this single collection - two differently-named
// collections still run in parallel with each other, which is what let a test that
// deliberately poisons SnowflakeCore._state break a concurrent id generation.
[Collection("SnowflakeStatics")]
public sealed class SnowflakeDITests : IDisposable
{
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
    public void AddSnowflakeWorkerId_Literal_PublishesImmediately()
    {
        TypedIdRuntime.SnowflakeProvider = null;

        new ServiceCollection().AddSnowflakeWorkerId(workerId: 42);

        // No host was built or started — the id is available as soon as registration returns.
        Assert.NotNull(TypedIdRuntime.SnowflakeProvider);
        Assert.Equal(42, TypedIdRuntime.SnowflakeProvider!.WorkerId);
    }

    [Fact]
    public void AddSnowflakeWorkerId_EnvVar_ReadsFromEnvironment()
    {
        TypedIdRuntime.SnowflakeProvider = null;
        Environment.SetEnvironmentVariable("ZA_TEST_WORKER", "7");
        try
        {
            new ServiceCollection().AddSnowflakeWorkerId(envVar: "ZA_TEST_WORKER");

            Assert.Equal(7, TypedIdRuntime.SnowflakeProvider!.WorkerId);
        }
        finally { Environment.SetEnvironmentVariable("ZA_TEST_WORKER", null); }
    }

    [Fact]
    public void AddSnowflakeWorkerId_EnvVar_MissingValue_UsesFallback()
    {
        TypedIdRuntime.SnowflakeProvider = null;
        Environment.SetEnvironmentVariable("ZA_MISSING_WORKER", null);

        new ServiceCollection().AddSnowflakeWorkerId(envVar: "ZA_MISSING_WORKER", fallback: 3);

        Assert.Equal(3, TypedIdRuntime.SnowflakeProvider!.WorkerId);
    }

    [Fact]
    public void AddSnowflakeWorkerId_Func_InvokesFactoryOnce()
    {
        TypedIdRuntime.SnowflakeProvider = null;
        int called = 0;

        new ServiceCollection().AddSnowflakeWorkerId(() => { called++; return 99; });

        Assert.Equal(1, called);
        Assert.Equal(99, TypedIdRuntime.SnowflakeProvider!.WorkerId);
    }

    [Fact]
    public void AddSnowflakeWorkerId_OutOfRangeId_ThrowsAtRegistration()
    {
        // Previously surfaced at host start. Publishing eagerly moves the failure to the call
        // site, which is both earlier and closer to the mistake.
        Assert.Throws<TypedIdException>(() => new ServiceCollection().AddSnowflakeWorkerId(workerId: 2048));
    }

    [Fact]
    public void AddSnowflakeWorkerId_OutOfRangeFromFactory_ThrowsAtRegistration()
        => Assert.Throws<TypedIdException>(() => new ServiceCollection().AddSnowflakeWorkerId(() => -1));

    [Fact]
    public void AddSnowflakeWorkerId_ReturnsSameCollectionForChaining()
    {
        var services = new ServiceCollection();

        Assert.Same(services, services.AddSnowflakeWorkerId(workerId: 1));
        Assert.Same(services, services.AddSnowflakeWorkerId(() => 2));
        Assert.Same(services, services.AddSnowflakeWorkerId(envVar: "ZA_UNSET_WORKER", fallback: 3));
    }
}
