using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("ZeroAlloc.ValueObjects.Tests")]
[assembly: InternalsVisibleTo("ZeroAlloc.ValueObjects.Benchmarks")]

// The hosted service backing AddSnowflakeWorkerId(Func<IServiceProvider, int>) lives in the
// Hosting package but shares this assembly's validate-and-publish path, so the two registration
// routes cannot drift. See SnowflakeWorkerIdPublisher.
[assembly: InternalsVisibleTo("ZeroAlloc.ValueObjects.Hosting")]
