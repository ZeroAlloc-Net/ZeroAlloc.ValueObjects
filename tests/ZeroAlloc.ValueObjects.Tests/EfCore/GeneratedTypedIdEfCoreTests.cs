using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ZeroAlloc.ValueObjects.EfCore;

namespace ZeroAlloc.ValueObjects.Tests.EfCore;

// The converters registered by the methods the generator emits: AddTypedIdConvention on each
// struct, and AddZeroAllocValueObjectsTestsTypedIdConventions for the whole test assembly.
public sealed class GeneratedTypedIdEfCoreTests : TypedIdEfCoreTests
{
    protected override TestDbContext CreateContext(SqliteConnection connection) =>
        new GeneratedTestDbContext(new DbContextOptionsBuilder<GeneratedTestDbContext>().UseSqlite(connection).Options);

    [Fact]
    public void PerIdMethod_ConfiguresOnlyItsOwnType()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        using var db = new SingleIdDbContext(new DbContextOptionsBuilder<SingleIdDbContext>().UseSqlite(connection).Options);

        var orderId = db.Model.FindEntityType(typeof(Order))!.FindProperty(nameof(Order.Id))!;
        Assert.IsType<TypedIdValueConverter<EfOrderId, Guid>>(orderId.GetValueConverter());
    }

    [Fact]
    public void PerIdMethod_RejectsNullBuilder() =>
        Assert.Throws<ArgumentNullException>("builder", () => EfOrderId.AddTypedIdConvention(null!));

    [Fact]
    public void ConventionsMethod_RejectsNullBuilder() =>
        Assert.Throws<ArgumentNullException>(
            "builder",
            () => ZeroAllocValueObjectsTestsTypedIdModelConfigurationExtensions.AddZeroAllocValueObjectsTestsTypedIdConventions(null!));

    private sealed class SingleIdDbContext : DbContext
    {
        public SingleIdDbContext(DbContextOptions<SingleIdDbContext> options) : base(options) { }

        public DbSet<Order> Orders => Set<Order>();

        protected override void ConfigureConventions(ModelConfigurationBuilder builder) =>
            EfOrderId.AddTypedIdConvention(builder);
    }
}
