using System.Linq;

namespace ZeroAlloc.ValueObjects.Tests;

/// <summary>
/// Guards ValueObjects#58: ZeroAlloc.ValueObjects must not depend on
/// Microsoft.Extensions.Hosting.Abstractions.
/// <para>
/// That single edge pulled Configuration.Abstractions, Diagnostics.Abstractions,
/// FileProviders.Abstractions and Options along with it, and because <c>[ValueObject]</c> tends to
/// be used in a solution's most foundational assembly, the cost reached every downstream consumer.
/// It existed only for the hosted service behind one <c>AddSnowflakeWorkerId</c> overload, which
/// now lives in ZeroAlloc.ValueObjects.Hosting.
/// </para>
/// <para>
/// A csproj PackageReference is easy to reintroduce by reflex — for a hosted service, an
/// <c>IHostApplicationLifetime</c> hook, or an <c>IHostEnvironment</c> check. This test fails the
/// build in that repository rather than surfacing the regression in a consumer's dependency graph.
/// </para>
/// </summary>
public sealed class CorePackageDependencyTests
{
    // Referenced-assembly metadata lists only assemblies the compiler actually bound against, so
    // an unused PackageReference would not trip this. That is the intended sensitivity: the
    // failure mode being guarded is code in this assembly using a hosting type again.
    [Theory]
    [InlineData("Microsoft.Extensions.Hosting.Abstractions")]
    [InlineData("Microsoft.Extensions.Configuration.Abstractions")]
    [InlineData("Microsoft.Extensions.FileProviders.Abstractions")]
    public void CoreAssembly_DoesNotReference(string assemblyName)
    {
        var referenced = typeof(TypedIdRuntime).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToList();

        Assert.DoesNotContain(assemblyName, referenced, StringComparer.Ordinal);
    }

    [Fact]
    public void CoreAssembly_StillReferencesDependencyInjectionAbstractions()
    {
        // The AddSnowflakeWorkerId overloads that stayed behind are still IServiceCollection
        // extensions, so this dependency is expected — it is Hosting that had to go, and this
        // assertion keeps the test above honest about what it is really proving.
        var referenced = typeof(TypedIdRuntime).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToList();

        Assert.Contains("Microsoft.Extensions.DependencyInjection.Abstractions", referenced, StringComparer.Ordinal);
    }
}
