using System.Runtime.CompilerServices;
using VerifyXunit;

namespace ZeroAlloc.ValueObjects.Tests.GeneratorTests;

public static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Init() => VerifySourceGenerators.Initialize();
}
