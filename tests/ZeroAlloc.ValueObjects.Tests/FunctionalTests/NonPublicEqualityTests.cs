namespace ZeroAlloc.ValueObjects.Tests.FunctionalTests;

/// <summary>
/// Regression for #169 — [EqualityMember] on a non-public member was silently
/// dropped, so equality quietly widened and two objects that should differ
/// compared equal. Nothing warned.
/// </summary>
public class NonPublicEqualityTests
{
    [Fact]
    public void PrivateMarkedMember_Participates()
    {
        var a = new MixedVisibilityVo("same", "secret A");
        var b = new MixedVisibilityVo("same", "secret B");

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void PrivateMarkedMember_EqualWhenAllMarkedMatch()
    {
        var a = new MixedVisibilityVo("same", "same secret");
        var b = new MixedVisibilityVo("same", "same secret");

        Assert.Equal(a, b);
    }

    [Fact]
    public void AllMarksNonPublic_DoesNotFallBackToPublicProperties()
    {
        // Loose is public and unmarked, so it must not affect equality. Under the
        // old behaviour the generator saw zero marks and compared it anyway.
        var a = new AllPrivateMarksVo("same key", "differs");
        var b = new AllPrivateMarksVo("same key", "also differs");

        Assert.Equal(a, b);
    }

    [Fact]
    public void AllMarksNonPublic_StillDistinguishesOnTheMarkedMember()
    {
        var a = new AllPrivateMarksVo("key A", "same");
        var b = new AllPrivateMarksVo("key B", "same");

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void NormalisingHelper_TreatsNullAndEmptyAsEqual()
    {
        // The motivating case: null-normalise through a private helper so null
        // and "" compare equal. Previously the helper was ignored entirely and
        // equality fell back to the public Branch, making these differ.
        var a = new NormalisedVo(null);
        var b = new NormalisedVo(string.Empty);

        Assert.Equal(a, b);
    }

    [Fact]
    public void NormalisingHelper_StillDistinguishesRealValues()
    {
        var a = new NormalisedVo("north");
        var b = new NormalisedVo("south");

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void InternalMarkedMember_Participates()
    {
        var a = new InternalMarkVo("x");
        var b = new InternalMarkVo("y");

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void PrivateMarkedMember_ContributesToHashCode()
    {
        var a = new MixedVisibilityVo("same", "secret A");
        var b = new MixedVisibilityVo("same", "secret B");

        Assert.NotEqual(a.GetHashCode(), b.GetHashCode());
    }
}
