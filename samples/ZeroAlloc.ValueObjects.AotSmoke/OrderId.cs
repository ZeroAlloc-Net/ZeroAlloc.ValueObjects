using ZeroAlloc.ValueObjects;

namespace ZeroAlloc.ValueObjects.AotSmoke;

[TypedId(Strategy = IdStrategy.Ulid)]
public readonly partial record struct OrderId;
