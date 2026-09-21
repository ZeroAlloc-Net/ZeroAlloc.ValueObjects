using ZeroAlloc.ValueObjects;

namespace ZeroAlloc.ValueObjects.Tests.FunctionalTests;

/// <summary>
/// Mixed public and private marks. Regression for #169: the private mark used
/// to be dropped, silently widening equality to the public marks alone.
/// </summary>
[ValueObject]
public partial class MixedVisibilityVo
{
    [EqualityMember] public string Public { get; }
    [EqualityMember] private string Secret { get; }

    public MixedVisibilityVo(string pub, string secret) => (Public, Secret) = (pub, secret);
}
