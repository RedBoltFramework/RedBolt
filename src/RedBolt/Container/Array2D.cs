using System.Collections;
using System.Diagnostics;

namespace RedBolt;

/// <summary>
/// 2D indexing utilities for row-major 2D array.
/// </summary>
public class ArrayDimensions2D
{
    /// <summary>
    /// Construct default.
    /// </summary>
    public ArrayDimensions2D()
    {
    }

    /// <summary>
    /// Construct with given size.
    /// </summary>
    public ArrayDimensions2D(int width, int height)
    {
        _width = width;
        _height = height;
    }

    /// <summary>
    /// Construct with given size as vector (implicitly convertible).
    /// </summary>
    public ArrayDimensions2D(Vector2i size)
        : this(size.X, size.Y)
    {
    }

    /// <summary>
    /// Return width.
    /// </summary>
    public int GetWidth() => _width;

    /// <summary>
    /// Return height.
    /// </summary>
    public int GetHeight() => _height;

    /// <summary>
    /// Return array size as int vector.
    /// </summary>
    public Vector2i GetSize() => new(_width, _height);

    /// <summary>
    /// Return array dimensions as IntRect.
    /// </summary>
    public IntRect GetRect() => new(Vector2i.Zero, GetSize());

    /// <summary>
    /// Return total number of elements.
    /// </summary>
    public uint GetCapacity() => unchecked((uint)(_width * _height));

    /// <summary>
    /// Return whether the index is contained in the array.
    /// </summary>
    public bool Contains(Vector2i index) => index.X >= 0 && index.Y >= 0 && index.X < _width && index.Y < _height;

    /// <summary>
    /// Return index clamped to array boundaries.
    /// </summary>
    public Vector2i ClampIndex(Vector2i index) => new(
        Math.Min(Math.Max(index.X, 0), _width - 1),
        Math.Min(Math.Max(index.Y, 0), _height - 1));

    /// <summary>
    /// Return index wrapped to array boundaries.
    /// </summary>
    public Vector2i WrapIndex(Vector2i index) => new(
        MathDefs.AbsMod(index.X, _width),
        MathDefs.AbsMod(index.Y, _height));

    /// <summary>
    /// Convert 2D array index to linear array index.
    /// </summary>
    public uint IndexToOffset(Vector2i index)
    {
        Debug.Assert(Contains(index));
        return unchecked((uint)(index.Y * _width + index.X));
    }

    /// <summary>
    /// Convert linear array index to 2D array index.
    /// </summary>
    public Vector2i OffsetToIndex(uint index)
    {
        Debug.Assert(index < GetCapacity());
        return new Vector2i(unchecked((int)(index % (uint)_width)), unchecked((int)(index / (uint)_width)));
    }

    /// <summary>
    /// Set dimensions.
    /// </summary>
    protected void SetSize(int width, int height)
    {
        _width = width;
        _height = height;
    }

    /// <summary>
    /// Swap with other array dimensions.
    /// </summary>
    protected void Swap(ArrayDimensions2D other)
    {
        (_width, other._width) = (other._width, _width);
        (_height, other._height) = (other._height, _height);
    }

    /// <summary>
    /// Array width.
    /// </summary>
    private int _width;

    /// <summary>
    /// Array height.
    /// </summary>
    private int _height;
}

/// <summary>
/// 2D array template.
/// </summary>
// C++: template <class T, class Container = ea::vector<T>> class Array2D : public ArrayDimensions2D
public class Array2D<T> : ArrayDimensions2D, IEnumerable<T>
{
    /// <summary>
    /// Construct default.
    /// </summary>
    public Array2D()
    {
        _data = [];
    }

    /// <summary>
    /// Construct with dimensions and default value.
    /// </summary>
    public Array2D(ArrayDimensions2D dim)
        : base(dim.GetWidth(), dim.GetHeight())
    {
        _data = new T[checked((int)GetCapacity())];
    }

    /// <summary>
    /// Construct with dimensions and initial value.
    /// </summary>
    public Array2D(ArrayDimensions2D dim, T value)
        : base(dim.GetWidth(), dim.GetHeight())
    {
        _data = new T[checked((int)GetCapacity())];
        Fill(value);
    }

    /// <summary>
    /// Construct with given size and default value.
    /// </summary>
    public Array2D(int width, int height)
        : base(width, height)
    {
        _data = new T[checked((int)GetCapacity())];
    }

    /// <summary>
    /// Construct with given size and initial value.
    /// </summary>
    public Array2D(int width, int height, T value)
        : base(width, height)
    {
        _data = new T[checked((int)GetCapacity())];
        Fill(value);
    }

    /// <summary>
    /// Reset array to empty.
    /// </summary>
    public void Reset() => Reset(0, 0);

    /// <summary>
    /// Resize array. All elements are reset to default value.
    /// </summary>
    public void Reset(ArrayDimensions2D dim) => Reset(dim.GetWidth(), dim.GetHeight());

    /// <summary>
    /// Resize array. All elements are reset to specified default value.
    /// </summary>
    public void Reset(ArrayDimensions2D dim, T value) => Reset(dim.GetWidth(), dim.GetHeight(), value);

    /// <summary>
    /// Resize array. All elements are reset to default value.
    /// </summary>
    public void Reset(int width, int height)
    {
        SetSize(width, height);
        _data = new T[checked((int)GetCapacity())];
    }

    /// <summary>
    /// Resize array. All elements are reset to specified default value.
    /// </summary>
    public void Reset(int width, int height, T value)
    {
        SetSize(width, height);
        _data = new T[checked((int)GetCapacity())];
        Fill(value);
    }

    /// <summary>
    /// Fill array with given value.
    /// </summary>
    public void Fill(T value) => Array.Fill(_data, value);

    /// <summary>
    /// Return array dimensions.
    /// </summary>
    // C++: const ArrayDimensions2D& Dim() const { return *this; }
    public ArrayDimensions2D Dim() => this;

    /// <summary>
    /// Return underlying container.
    /// </summary>
    // C++: const Container& GetContainer() const { return data_; }  (const overload)
    public T[] GetContainer() => _data;

    /// <summary>
    /// Return element by index.
    /// </summary>
    // C++: const T& Get(const IntVector2& index) const { return data_[IndexToOffset(index)]; }  (const overload)
    public ref T Get(Vector2i index) => ref _data[checked((int)IndexToOffset(index))];

    /// <summary>
    /// Return element by index or null if out of bounds.
    /// </summary>
    // C++: T* GetOptional(const IntVector2& index) { return Contains(index) ? &data_[IndexToOffset(index)] : nullptr; }
    // C++: const T* GetOptional(const IntVector2& index) const { ... }  (const overload)
    // TODO: port - C++ returns pointer (T*), C# returns nullable value (T?)
    public T? GetOptional(Vector2i index) => Contains(index) ? _data[checked((int)IndexToOffset(index))] : default;

    /// <summary>
    /// Return element by wrapped index.
    /// </summary>
    // C++: const T& GetWrapped(const IntVector2& index) const { ... }  (const overload)
    public ref T GetWrapped(Vector2i index) => ref _data[checked((int)IndexToOffset(WrapIndex(index)))];

    /// <summary>
    /// Return element by clamped index.
    /// </summary>
    // C++: const T& GetClamped(const IntVector2& index) const { ... }  (const overload)
    public ref T GetClamped(Vector2i index) => ref _data[checked((int)IndexToOffset(ClampIndex(index)))];

    /// <summary>
    /// Return element by index.
    /// </summary>
    // C++: const T& operator[](const IntVector2& index) const { return Get(index); }  (const overload)
    public ref T this[Vector2i index] => ref Get(index);

    /// <summary>
    /// Swap with other array.
    /// </summary>
    public void Swap(Array2D<T> other)
    {
        base.Swap(other);
        (_data, other._data) = (other._data, _data);
    }

    /// <summary>
    /// Return array begin.
    /// </summary>
    // C++: auto Begin() const { return data_.cbegin(); }  (const overload)
    public Span<T> Begin() => _data;

    /// <summary>
    /// Return array end.
    /// </summary>
    // C++: auto End() const { return data_.cend(); }  (const overload)
    public Span<T> End() => _data.AsSpan(_data.Length);

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)_data).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Array data.
    /// </summary>
    private T[] _data;
}
