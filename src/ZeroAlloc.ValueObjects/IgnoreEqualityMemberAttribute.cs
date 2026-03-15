using System;

namespace ZeroAlloc.ValueObjects;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class IgnoreEqualityMemberAttribute : Attribute { }
