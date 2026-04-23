using ZeroAlloc.ValueObjects;

namespace ZeroAlloc.ValueObjects.AotSmoke;

[TypedId(Strategy = IdStrategy.Uuid7)]
public readonly partial record struct MessageId;
