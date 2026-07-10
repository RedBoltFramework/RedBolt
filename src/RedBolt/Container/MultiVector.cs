namespace RedBolt;

public class MultiVector<T> : IEnumerable<T>
{
    public readonly record struct Index(uint Outer, uint Inner);

    private readonly List<List<T>> _values = [];

    public void Clear(uint outerSize)
    {
        _values.Clear();
        for (var i = 0u; i < outerSize; ++i)
            _values.Add([]);
    }

    public T EmplaceBack(uint outerIndex, Func<T> factory)
    {
        var value = factory();
        _values[(int)outerIndex].Add(value);
        return value;
    }

    public Index PushBack(uint outerIndex, T value)
    {
        var inner = (uint)_values[(int)outerIndex].Count;
        _values[(int)outerIndex].Add(value);
        return new Index(outerIndex, inner);
    }

    public void PopBack(uint outerIndex) => _values[(int)outerIndex].RemoveAt(_values[(int)outerIndex].Count - 1);

    public uint Size()
    {
        var size = 0u;
        foreach (var values in _values)
            size += (uint)values.Count;
        return size;
    }

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

    public void CopyTo(List<T> dest)
    {
        foreach (var values in _values)
            dest.AddRange(values);
    }

    public T this[Index index] => _values[(int)index.Outer][(int)index.Inner];

    public IReadOnlyList<List<T>> GetUnderlyingCollection() => _values;

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
