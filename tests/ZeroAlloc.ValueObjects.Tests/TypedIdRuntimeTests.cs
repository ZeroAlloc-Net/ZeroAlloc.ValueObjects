namespace ZeroAlloc.ValueObjects.Tests;

// Shares a collection with other tests that mutate TypedIdRuntime.SnowflakeProvider to
// avoid parallel-class races on the static provider slot. This class asserts the slot's exact
// contents (Assert.Null, Assert.Same), so it cannot tolerate a concurrent writer at all.
[Collection("SnowflakeStatics")]
public sealed class TypedIdRuntimeTests
{
    [Fact]
    public void SnowflakeProvider_CanBeSetAndCleared()
    {
        // Runtime is static — save and restore to avoid cross-test contamination.
        var original = TypedIdRuntime.SnowflakeProvider;
        try
        {
            TypedIdRuntime.SnowflakeProvider = null;
            Assert.Null(TypedIdRuntime.SnowflakeProvider);

            var provider = new StubProvider(42);
            TypedIdRuntime.SnowflakeProvider = provider;
            Assert.Same(provider, TypedIdRuntime.SnowflakeProvider);
            Assert.Equal(42, TypedIdRuntime.SnowflakeProvider!.WorkerId);
        }
        finally { TypedIdRuntime.SnowflakeProvider = original; }
    }

    [Fact]
    public void TypedIdException_CarriesMessage()
    {
        var ex = new TypedIdException("boom");
        Assert.Equal("boom", ex.Message);
        Assert.IsAssignableFrom<InvalidOperationException>(ex);
    }

    private sealed class StubProvider : ISnowflakeWorkerIdProvider
    {
        public StubProvider(int id) => WorkerId = id;
        public int WorkerId { get; }
    }
}
