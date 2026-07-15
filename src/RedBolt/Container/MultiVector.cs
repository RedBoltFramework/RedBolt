namespace RedBolt;

public class MultiVector<T> : IEnumerable<T>
{
    // C++: using Index = ea::pair<unsigned, unsigned>;
    public readonly record struct Index(uint Outer, uint Inner);

    private readonly List<List<T>> _values = [];

    // C++: outer_.resize(outerSize); then for each inner: inner.clear();
    public void Clear(uint outerSize)
    {
        _values.Clear();
        for (var i = 0u; i < outerSize; ++i)
            _values.Add([]);
    }

    // C++: template <class ... Args> T& EmplaceBack(unsigned outerIndex, Args&& ... args)
    // TODO: port - C++ uses variadic template arguments forwarded to inner emplace_back; C# uses factory Func<T>
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

    // C++: void Resize(int outerSize) { outer_.resize(outerSize); }
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
        dest.Clear();
        foreach (var values in _values)
            dest.AddRange(values);
    }

    // C++: const T& operator[](const Index& index) const { return outer_[index.first][index.second]; }
    public T this[Index index] => _values[(int)index.Outer][(int)index.Inner];

    // C++: OuterCollection& GetUnderlyingCollection() { return outer_; } _and_ const overload
    public List<List<T>> GetUnderlyingCollection() => _values;

    // TODO: port - C++ has Begin() and End() methods (4 overloads: mutable/const begin, mutable/const end)
    // TODO: port - C++ has BaseIterator<>, Iterator, ConstIterator nested types for forward-iterator support

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
