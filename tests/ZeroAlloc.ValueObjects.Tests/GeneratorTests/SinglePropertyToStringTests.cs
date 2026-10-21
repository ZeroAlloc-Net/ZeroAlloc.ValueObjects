using ZeroAlloc.ValueObjects.Generator;

namespace ZeroAlloc.ValueObjects.Tests.GeneratorTests;

/// <summary>
/// A value object with a single equality member returns that member's string form from
/// ToString. The generated call must exist on the member's type: Guid and TimeSpan have no
/// ToString(IFormatProvider) overload, unlike int or DateTime.
/// </summary>
public class SinglePropertyToStringTests
{
    [Theory]
    [InlineData("System.Guid")]
    [InlineData("System.TimeSpan")]
    [InlineData("System.Guid?")]
    [InlineData("System.TimeSpan?")]
    [InlineData("int")]
    [InlineData("System.DateTimeOffset")]
    public void Single_member_value_object_compiles(string memberType)
    {
        var run = GeneratorRun.Of(new ValueObjectGenerator(), $$"""
            using ZeroAlloc.ValueObjects;
            namespace App
            {
                [ValueObject]
                public readonly partial struct Id
                {
                    public Id({{memberType}} value) => Value = value;
                    public {{memberType}} Value { get; }
                }
            }
            """);

        Assert.Empty(run.GeneratorDiagnostics);
        Assert.Empty(run.Errors);
        Assert.Empty(run.Warnings);
    }
}
