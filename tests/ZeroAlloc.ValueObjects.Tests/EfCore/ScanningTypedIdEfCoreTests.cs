using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ZeroAlloc.ValueObjects.EfCore;

namespace ZeroAlloc.ValueObjects.Tests.EfCore;

// The converters registered by the reflection-based assembly scan.
public sealed class ScanningTypedIdEfCoreTests : TypedIdEfCoreTests
{
    protected override TestDbContext CreateContext(SqliteConnection connection) =>
        new ScanningTestDbContext(new DbContextOptionsBuilder<ScanningTestDbContext>().UseSqlite(connection).Options);
}
