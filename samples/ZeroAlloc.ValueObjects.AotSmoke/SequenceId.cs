using ZeroAlloc.ValueObjects;

namespace ZeroAlloc.ValueObjects.AotSmoke;

[TypedId(Strategy = IdStrategy.Sequential)]
public readonly partial record struct SequenceId;
