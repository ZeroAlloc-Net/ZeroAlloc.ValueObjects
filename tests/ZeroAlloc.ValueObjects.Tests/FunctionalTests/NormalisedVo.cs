using ZeroAlloc.ValueObjects;

namespace ZeroAlloc.ValueObjects.Tests.FunctionalTests;

/// <summary>
/// The realistic route in: null-normalising through a private helper before
/// comparing. Compiles cleanly, reads correctly, and used not to participate.
/// </summary>
[ValueObject]
public partial class NormalisedVo
{
    public string? Branch { get; }
    [EqualityMember] private string BranchForEquality => Branch ?? string.Empty;

    public NormalisedVo(string? branch) => Branch = branch;
}
