using ZeroAlloc.ValueObjects;

namespace ZeroAlloc.ValueObjects.Tests.FunctionalTests;

/// <summary>
/// Every mark non-public. The worse of the two failure modes: the generator saw
/// no marked members at all and fell back to comparing raw public properties,
/// which is a different equality than the author wrote.
/// </summary>
[ValueObject]
public partial class AllPrivateMarksVo
{
    [EqualityMember] private string Key { get; }
    public string Loose { get; }

    public AllPrivateMarksVo(string key, string loose) => (Key, Loose) = (key, loose);
}
