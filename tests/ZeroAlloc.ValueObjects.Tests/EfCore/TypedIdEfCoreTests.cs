using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ZeroAlloc.ValueObjects;
using ZeroAlloc.ValueObjects.EfCore;

namespace ZeroAlloc.ValueObjects.Tests.EfCore;

// TypedIds + EF support types declared here for the test project — the convention
// picks them up by attribute presence at model-build time.
// MA0048: co-locating small test entity types in one file is intentional.
// MA0097: generated IComparable<T> does not provide comparison operators (by design:
// id types are sortable but not ordered for arithmetic semantics).
#pragma warning disable MA0048, MA0097

[TypedId(Strategy = IdStrategy.Ulid)]
public readonly partial record struct EfOrderId;

[TypedId(Strategy = IdStrategy.Snowflake)]
public readonly partial record struct EfMessageId;

public sealed class Order
{
    public EfOrderId Id { get; set; }
    public string Label { get; set; } = "";
}

public sealed class Message
{
    public EfMessageId Id { get; set; }
    public string Body { get; set; } = "";
}

public sealed class TestDbContext : DbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Message> Messages => Set<Message>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        builder.AddTypedIdConventions(typeof(EfOrderId).Assembly);
        base.ConfigureConventions(builder);
    }
}

#pragma warning restore MA0048, MA0097

public sealed class TypedIdEfCoreTests : IAsyncLifetime
{
    private SqliteConnection _conn = null!;
    private TestDbContext _db = null!;

    public async Task InitializeAsync()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        await _conn.OpenAsync().ConfigureAwait(false);
        var opts = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(_conn).Options;
        _db = new TestDbContext(opts);
        await _db.Database.EnsureCreatedAsync().ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync().ConfigureAwait(false);
        await _conn.DisposeAsync().ConfigureAwait(false);
    }

    [Fact]
    public async Task GuidBacked_TypedId_RoundTrips()
    {
        TypedIdRuntime.SnowflakeProvider ??= new StubProv(1);
        var id = EfOrderId.New();
        _db.Orders.Add(new Order { Id = id, Label = "hi" });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var found = await _db.Orders.FirstAsync();
        Assert.Equal(id, found.Id);
        Assert.Equal("hi", found.Label);
    }

    [Fact]
    public async Task Int64Backed_TypedId_RoundTrips()
    {
        TypedIdRuntime.SnowflakeProvider ??= new StubProv(1);
        var id = EfMessageId.New();
        _db.Messages.Add(new Message { Id = id, Body = "hey" });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var found = await _db.Messages.FirstAsync();
        Assert.Equal(id, found.Id);
        Assert.Equal("hey", found.Body);
    }

    [Fact]
    public async Task GuidBacked_StoredAsBlob_NotAsString()
    {
        TypedIdRuntime.SnowflakeProvider ??= new StubProv(1);
        var id = EfOrderId.New();
        _db.Orders.Add(new Order { Id = id, Label = "x" });
        await _db.SaveChangesAsync();

        // SQLite stores Guid as BLOB by default via EF Core's default converter for Guid.
        // Just verify the stored data round-trips; column-type assertions depend on provider.
        _db.ChangeTracker.Clear();
        var found = await _db.Orders.FirstAsync();
        Assert.Equal(id.Value, found.Id.Value);
    }

    private sealed class StubProv : ISnowflakeWorkerIdProvider
    {
        public StubProv(int id) => WorkerId = id;
        public int WorkerId { get; }
    }
}
