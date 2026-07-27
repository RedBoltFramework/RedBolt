using System.Runtime.InteropServices;

namespace RedBolt.Container;

/// <summary>Vector of vectors.</summary>
public class MultiVector<T> : IEnumerable<T>, ICloneable, IDisposable
{
    /// <summary>Index in multi-vector (pair of outer and inner indices).</summary>
    public readonly record struct Index(int Outer, int Inner);

    private readonly List<List<T>> _values = [];

    private static readonly bool IsCloneable = typeof(ICloneable).IsAssignableFrom(typeof(T));
    private static readonly bool IsDisposableValue = IsCloneable && typeof(IDisposable).IsAssignableFrom(typeof(T));

    /// <summary>Clear inner vectors. Reset outer vector to fixed size.</summary>
    public void Clear(int outerSize)
    {
        Resize(outerSize);
        foreach (var values in _values)
        {
            DisposeValues(values);
            values.Clear();
        }
    }

    /// <summary>Emplace element at the back of specified outer vector.</summary>
    /// <remarks>C++ forwards variadic constructor arguments; the managed equivalent accepts a factory.</remarks>
    public ref T EmplaceBack(int outerIndex, Func<T> factory)
    {
        var value = factory();
        var values = _values[outerIndex];
        values.Add(value);
        return ref CollectionsMarshal.AsSpan(values)[^1];
    }

    /// <summary>Push element into back of specified outer vector. Return index of added element.</summary>
    public Index PushBack(int outerIndex, T value)
    {
        var inner = _values[outerIndex].Count;
        _values[outerIndex].Add(CopyValue(value));
        return new Index(outerIndex, inner);
    }

    /// <summary>Pop element from back of specified outer vector.</summary>
    public void PopBack(int outerIndex)
    {
        var values = _values[outerIndex];
        var index = values.Count - 1;
        DisposeValue(values[index]);
        values.RemoveAt(index);
    }

    /// <summary>Return size.</summary>
    public int Size()
    {
        var size = 0;
        foreach (var values in _values)
            size += values.Count;
        return size;
    }

    /// <summary>Resize outer vector.</summary>
    public void Resize(int outerSize)
    {
        if (outerSize < _values.Count)
        {
            for (var i = outerSize; i < _values.Count; ++i)
                DisposeValues(_values[i]);
            _values.RemoveRange(outerSize, _values.Count - outerSize);
        }
        else
        {
            while (_values.Count < outerSize)
                _values.Add([]);
        }
    }

    /// <summary>Copy content to vector.</summary>
    public void CopyTo(List<T> dest)
    {
        DisposeValues(dest);
        dest.Clear();
        foreach (var values in _values)
        {
            foreach (var value in values)
                dest.Add(CopyValue(value));
        }
    }

    /// <summary>Return element.</summary>
    public ref T this[Index index] =>
        ref CollectionsMarshal.AsSpan(_values[index.Outer])[index.Inner];

    /// <summary>Return outer collection.</summary>
    public List<List<T>> GetUnderlyingCollection() => _values;

    public object Clone()
    {
        var result = new MultiVector<T>();
        foreach (var values in _values)
        {
            var resultValues = new List<T>(values.Count);
            foreach (var value in values)
                resultValues.Add(CopyValue(value));
            result._values.Add(resultValues);
        }
        return result;
    }

    public void Dispose()
    {
        foreach (var values in _values)
            DisposeValues(values);
        _values.Clear();
    }

    /// <summary>Enumerate all elements in outer-vector order.</summary>
    public IEnumerator<T> GetEnumerator()
    {
        foreach (var values in _values)
        {
            foreach (var value in values)
                yield return value;
        }
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    private static T CopyValue(T value)
    {
        if (!IsCloneable)
            return value;
        return (T)((ICloneable)(object)value!).Clone();
    }

    private static void DisposeValue(T value)
    {
        if (IsDisposableValue)
            ((IDisposable)(object)value!).Dispose();
    }

    private static void DisposeValues(List<T> values)
    {
        if (!IsDisposableValue)
            return;
        foreach (var value in values)
            ((IDisposable)(object)value!).Dispose();
    }
}
