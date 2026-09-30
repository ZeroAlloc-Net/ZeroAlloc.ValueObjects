using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace ZeroAlloc.ValueObjects.Generator.Models;

/// <summary>
/// An immutable array that compares by its items, so a model that holds one can be cached by the
/// incremental pipeline. <see cref="ImmutableArray{T}"/> compares by reference: a model holding
/// one never compares equal to the model of the previous run.
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
    where T : IEquatable<T>
{
    private readonly ImmutableArray<T> _items;

    public EquatableArray(ImmutableArray<T> items) => _items = items;

    public bool IsEmpty => _items.IsDefaultOrEmpty;

    public int Count => _items.IsDefault ? 0 : _items.Length;

    public T this[int index] => _items[index];

    public ImmutableArray<T>.Enumerator GetEnumerator() =>
        (_items.IsDefault ? ImmutableArray<T>.Empty : _items).GetEnumerator();

    public bool Equals(EquatableArray<T> other)
    {
        var left = _items.IsDefault ? ImmutableArray<T>.Empty : _items;
        var right = other._items.IsDefault ? ImmutableArray<T>.Empty : other._items;
        if (left.Length != right.Length) return false;
        for (var i = 0; i < left.Length; i++)
        {
            if (!EqualityComparer<T>.Default.Equals(left[i], right[i])) return false;
        }
        return true;
    }

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        var hash = 17;
        foreach (var item in this)
            hash = unchecked((hash * 31) + (item is null ? 0 : EqualityComparer<T>.Default.GetHashCode(item)));
        return hash;
    }

    public T[] ToArray() => _items.IsDefault ? Array.Empty<T>() : _items.ToArray();

    IEnumerator<T> IEnumerable<T>.GetEnumerator() =>
        ((IEnumerable<T>)(_items.IsDefault ? ImmutableArray<T>.Empty : _items)).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<T>)this).GetEnumerator();

    public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right) => left.Equals(right);

    public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right) => !left.Equals(right);
}
