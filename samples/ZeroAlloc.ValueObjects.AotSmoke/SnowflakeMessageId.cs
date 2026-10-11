using ZeroAlloc.ValueObjects;

namespace ZeroAlloc.ValueObjects.AotSmoke;

[TypedId(Strategy = IdStrategy.Snowflake)]
public readonly partial record struct SnowflakeMessageId;
