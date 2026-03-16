using System;

namespace ZeroAlloc.ValueObjects;

[AttributeUsage(AttributeTargets.Property)]
public sealed class IgnoreEqualityMemberAttribute : Attribute { }
