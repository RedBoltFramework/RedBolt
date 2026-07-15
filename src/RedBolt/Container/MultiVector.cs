namespace RedBolt;

/// <summary>Vector of vectors.</summary>
public class MultiVector<T> : IEnumerable<T>
{
    // C++: using Index = ea::pair<unsigned, unsigned>;
    /// <summary>Index in multi-vector (pair of outer and inner indices).</summary>
    public readonly record struct Index(uint Outer, uint Inner);

    private readonly List<List<T>> _values = [];

    /// <summary>Clear inner vectors. Reset outer vector to fixed size.</summary>
    public void Clear(uint outerSize)
    {
        _values.Clear();
        for (var i = 0u; i < outerSize; ++i)
            _values.Add([]);
    }

    /// <summary>Emplace element at the back of specified outer vector.</summary>
    /// <remarks>C++ forwards variadic constructor arguments; the managed equivalent accepts a factory.</remarks>
    public T EmplaceBack(uint outerIndex, Func<T> factory)
    {
        var value = factory();
        _values[(int)outerIndex].Add(value);
        return value;
    }

    /// <summary>Push element into back of specified outer vector. Return index of added element.</summary>
    public Index PushBack(uint outerIndex, T value)
    {
        var inner = (uint)_values[(int)outerIndex].Count;
        _values[(int)outerIndex].Add(value);
        return new Index(outerIndex, inner);
    }

    /// <summary>Pop element from back of specified outer vector.</summary>
    public void PopBack(uint outerIndex) => _values[(int)outerIndex].RemoveAt(_values[(int)outerIndex].Count - 1);

    /// <summary>Return size.</summary>
    public uint Size()
    {
        var size = 0u;
        foreach (var values in _values)
            size += (uint)values.Count;
        return size;
    }

    /// <summary>Resize outer vector.</summary>
    public void Resize(int outerSize)
    {
        if (outerSize < _values.Count)
            _values.RemoveRange(outerSize, _values.Count - outerSize);
        else
        {
            while (_values.Count < outerSize)
                _values.Add([]);
        }
    }

    /// <summary>Copy content to vector.</summary>
    public void CopyTo(List<T> dest)
    {
        dest.Clear();
        foreach (var values in _values)
            dest.AddRange(values);
    }

    /// <summary>Return element.</summary>
    public T this[Index index] => _values[(int)index.Outer][(int)index.Inner];

    /// <summary>Return outer collection.</summary>
    public List<List<T>> GetUnderlyingCollection() => _values;

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
}
