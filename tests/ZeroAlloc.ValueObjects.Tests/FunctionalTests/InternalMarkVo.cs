using ZeroAlloc.ValueObjects;

namespace ZeroAlloc.ValueObjects.Tests.FunctionalTests;

/// <summary>Internal marks, same expectation as private.</summary>
[ValueObject]
public partial class InternalMarkVo
{
    [EqualityMember] internal string Value { get; }

    public InternalMarkVo(string value) => Value = value;
}
