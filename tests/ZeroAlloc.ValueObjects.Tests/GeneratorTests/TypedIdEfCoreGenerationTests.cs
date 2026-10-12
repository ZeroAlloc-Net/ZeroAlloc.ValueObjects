using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using ZeroAlloc.ValueObjects.Generator;

namespace ZeroAlloc.ValueObjects.Tests.GeneratorTests;

/// <summary>
/// For a compilation that references ZeroAlloc.ValueObjects.EfCore, the generator adds a static
/// <c>AddTypedIdConvention</c> to every [TypedId] struct and an <c>Add{Assembly}TypedIdConventions</c>
/// extension that calls them all, so the converters are registered without reflection or an
/// assembly scan (#205). Each run here also compiles the output, so a struct the conventions method
/// cannot name would fail with CS0122.
/// </summary>
public sealed class TypedIdEfCoreGenerationTests
{
    private const string ConventionsHintName = "ZeroAlloc-TypedIdConventions.EfCore.g.cs";

    [Fact]
    public void TopLevelTypedIds_GenerateTheirConventionAndTheAssemblyConventions()
    {
        var run = GeneratorRun.OfWithEfCore(new TypedIdGenerator(), """
            using ZeroAlloc.ValueObjects;
            namespace App;
            [TypedId(Strategy = IdStrategy.Ulid)] public readonly partial record struct OrderId;
            [TypedId(Strategy = IdStrategy.Snowflake)] internal readonly partial record struct MessageId;
            """);

        AssertClean(run);
        ZeroAlloc.TestHelpers.GeneratorSnapshot.Verify(run.Driver);
    }

    [Fact]
    public void NestedTypedIds_AreRegistered_WhenTheAssemblyCanNameThem()
    {
        var run = GeneratorRun.OfWithEfCore(new TypedIdGenerator(), """
            using ZeroAlloc.ValueObjects;
            using Microsoft.EntityFrameworkCore;
            namespace App
            {
                public partial class Orders
                {
                    [TypedId(Strategy = IdStrategy.Sequential)] public readonly partial record struct Id;
                    [TypedId] protected internal readonly partial record struct Line;
                    [TypedId] private readonly partial record struct Secret;

                    // A struct the conventions method cannot name is registered from inside its container.
                    public static void Register(ModelConfigurationBuilder builder) => Secret.AddTypedIdConvention(builder);
                }
                public partial class @class
                {
                    [TypedId] public readonly partial record struct @event;
                }
            }
            """);

        AssertClean(run);
        ZeroAlloc.TestHelpers.GeneratorSnapshot.Verify(run.Driver);
    }

    [Fact]
    public void WithoutEfCore_NoRegistrationIsGenerated()
    {
        var run = GeneratorRun.Of(new TypedIdGenerator(), """
            using ZeroAlloc.ValueObjects;
            namespace App;
            [TypedId] public readonly partial record struct OrderId;
            """);

        AssertClean(run);
        Assert.Equal(["App.OrderId.TypedId.g.cs"], run.HintNames);
    }

    [Fact]
    public void WithoutTypedIds_NoConventionsClassIsGenerated()
    {
        var run = GeneratorRun.OfWithEfCore(new TypedIdGenerator(), "namespace App; public sealed class Plain;");

        AssertClean(run);
        Assert.Empty(run.HintNames);
    }

    [Fact]
    public void TypedIdsThatAreNotGenerated_AreLeftOutOfTheConventions()
    {
        var run = GeneratorRun.OfWithEfCore(new TypedIdGenerator(), """
            using ZeroAlloc.ValueObjects;
            namespace App
            {
                [TypedId] public readonly partial record struct OrderId;
                [TypedId] public readonly partial record struct orderId;
                [TypedId] public readonly partial record struct Generic<T>;
            }
            """);

        Assert.Equal(["ZATI007", "ZATI009"], run.GeneratorDiagnostics.Select(d => d.Id).Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(
            ["App.OrderId.TypedId.EfCore.g.cs", "App.OrderId.TypedId.g.cs", ConventionsHintName],
            run.HintNames);
        var calls = Conventions(run).Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Contains(".AddTypedIdConvention(", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(["global::App.OrderId.AddTypedIdConvention(builder);"], calls);
    }

    [Theory]
    [InlineData("Internal", "internal")]
    [InlineData("internal", "internal")]
    [InlineData("Public", "public")]
    [InlineData("", "public")]
    public void ZeroAllocGeneratedAccessibility_SetsTheAccessibilityOfTheConventionsClass(string value, string keyword)
    {
        var run = GeneratorRun.OfWithEfCore(
            new TypedIdGenerator(),
            new Dictionary<string, string>(StringComparer.Ordinal) { ["build_property.ZeroAllocGeneratedAccessibility"] = value },
            "using ZeroAlloc.ValueObjects; namespace App; [TypedId] public readonly partial record struct OrderId;");

        AssertClean(run);
        var conventions = run.Output.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.GenTestTypedIdModelConfigurationExtensions");
        Assert.NotNull(conventions);
        var expected = string.Equals(keyword, "internal", StringComparison.Ordinal) ? Accessibility.Internal : Accessibility.Public;
        Assert.Equal(expected, conventions!.DeclaredAccessibility);
        Assert.Equal(expected, AssertEx.One(conventions.GetMembers("AddGenTestTypedIdConventions")).DeclaredAccessibility);

        // The member on the struct keeps the struct's accessibility.
        var member = AssertEx.One(run.Output.GetTypeByMetadataName("App.OrderId")!.GetMembers("AddTypedIdConvention"));
        Assert.Equal(Accessibility.Public, member.DeclaredAccessibility);
    }

    [Fact]
    public void InvalidZeroAllocGeneratedAccessibility_ReportsZati010_AndKeepsThePublicDefault()
    {
        var run = GeneratorRun.OfWithEfCore(
            new TypedIdGenerator(),
            new Dictionary<string, string>(StringComparer.Ordinal) { ["build_property.ZeroAllocGeneratedAccessibility"] = "Private" },
            "using ZeroAlloc.ValueObjects; namespace App; [TypedId] public readonly partial record struct OrderId;");

        var zati010 = AssertEx.One(run.GeneratorDiagnostics);
        Assert.Equal("ZATI010", zati010.Id);
        Assert.Equal(DiagnosticSeverity.Error, zati010.Severity);
        Assert.Equal(Location.None, zati010.Location);
        Assert.Equal(
            "MSBuild property 'ZeroAllocGeneratedAccessibility' has invalid value 'Private'; allowed values are 'Public' and 'Internal'",
            zati010.GetMessage(CultureInfo.InvariantCulture));
        Assert.Contains("public static class GenTestTypedIdModelConfigurationExtensions", Conventions(run), StringComparison.Ordinal);
    }

    private static string Conventions(GeneratorRun run) =>
        run.Driver.GetRunResult().Results[0].GeneratedSources
            .Single(s => string.Equals(s.HintName, ConventionsHintName, StringComparison.Ordinal))
            .SourceText.ToString();

    private static void AssertClean(GeneratorRun run)
    {
        Assert.Empty(run.GeneratorDiagnostics);
        Assert.Empty(run.Errors);
        Assert.Empty(run.Warnings);
    }
}
