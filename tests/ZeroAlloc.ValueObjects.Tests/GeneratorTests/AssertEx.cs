using System;
using System.Collections.Generic;
using System.Linq;

namespace ZeroAlloc.ValueObjects.Tests.GeneratorTests;

internal static class AssertEx
{
    /// <summary>Asserts that exactly one item matches, and returns it.</summary>
    public static T One<T>(IEnumerable<T> items, Func<T, bool> predicate)
    {
        var matches = items.Where(predicate).ToList();
        Assert.True(matches.Count == 1, $"Expected exactly one match, found {matches.Count}.");
        return matches[0];
    }
}
