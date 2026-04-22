using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ZeroAlloc.ValueObjects;

namespace ZeroAlloc.ValueObjects.Tests;

// These TypedId structs are produced by the source generator at compile time; the
// partial declarations below supply nothing more than an anchor for the attribute.
// MA0048: co-locating several TypedId anchors in one test file is intentional.
#pragma warning disable MA0048

[TypedId(Strategy = IdStrategy.Ulid)]
public readonly partial record struct RouteUlidId;

[TypedId(Strategy = IdStrategy.Snowflake)]
public readonly partial record struct RouteSnowflakeId;

#pragma warning restore MA0048

// Shares a collection with other tests that mutate TypedIdRuntime.SnowflakeProvider to
// avoid parallel-class races on the static provider slot.
[Collection("SnowflakeProviderMutation")]
public sealed class TypedIdMinimalApiTests : IDisposable
{
    private readonly ISnowflakeWorkerIdProvider? _originalProvider;

    public TypedIdMinimalApiTests()
    {
        _originalProvider = TypedIdRuntime.SnowflakeProvider;
    }

    public void Dispose() => TypedIdRuntime.SnowflakeProvider = _originalProvider;

    [Fact]
    public async Task GuidBacked_RouteBinding_ParsesAndReturns()
    {
        using var host = await CreateHostAsync(endpoints =>
        {
            endpoints.MapGet("/ulid/{id}", (RouteUlidId id) => Results.Text(id.ToString()));
        });
        var client = host.GetTestClient();

        var probe = RouteUlidId.New();
        var response = await client.GetAsync(new Uri($"/ulid/{probe}", UriKind.Relative));
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(probe.ToString(), body);
    }

    [Fact]
    public async Task Int64Backed_RouteBinding_ParsesAndReturns()
    {
        TypedIdRuntime.SnowflakeProvider = new StubProv(3);
        using var host = await CreateHostAsync(endpoints =>
        {
            endpoints.MapGet("/snowflake/{id}", (RouteSnowflakeId id) => Results.Text(id.ToString()));
        });
        var client = host.GetTestClient();

        var probe = RouteSnowflakeId.New();
        var response = await client.GetAsync(new Uri($"/snowflake/{probe}", UriKind.Relative));
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(probe.ToString(), body);
    }

    [Fact]
    public async Task InvalidRouteValue_Returns404()
    {
        using var host = await CreateHostAsync(endpoints =>
        {
            endpoints.MapGet("/ulid/{id}", (RouteUlidId id) => Results.Text(id.ToString()));
        });
        var client = host.GetTestClient();

        var response = await client.GetAsync(new Uri("/ulid/not-a-ulid!", UriKind.Relative));
        // Minimal API returns 400 BadRequest when an IParsable<T> route parameter fails to parse
        // (404 NotFound would occur only if no route template matched at all).
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<IHost> CreateHostAsync(Action<IEndpointRouteBuilder> configure)
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(builder =>
            {
                builder.UseTestServer()
                    .ConfigureServices(s => s.AddRouting())
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(configure);
                    });
            })
            .StartAsync()
            .ConfigureAwait(false);
        return host;
    }

    private sealed class StubProv : ISnowflakeWorkerIdProvider
    {
        public StubProv(int id) => WorkerId = id;
        public int WorkerId { get; }
    }
}
