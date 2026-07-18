using System;
using System.Collections;
using System.Collections.Generic;

namespace SharpCpp.Support;

public interface IFixedArray<T> : IList<T>
{
    int Length { get; }
}

public abstract class ArrayBase<T> : NativeObject, IFixedArray<T>
{
    protected ArrayBase(nint handle, int length, bool ownsHandle)
        : base(handle, ownsHandle)
    {
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        Length = length;
    }

    public int Length { get; }

    public int Count => Length;

    public bool IsReadOnly => false;

    public NativeArrayAbi ToNativeArrayAbi() => new(DangerousGetHandle(), (nuint)Length);

    public T this[int index]
    {
        get
        {
            ValidateIndex(index);
            return GetAtCore((nuint)index);
        }
        set
        {
            ValidateIndex(index);
            SetAtCore((nuint)index, value);
        }
    }

    protected abstract T GetAtCore(nuint index);

    protected abstract void SetAtCore(nuint index, T value);

    protected void Assign(ReadOnlySpan<T> values)
    {
        if (values.Length != Length)
        {
            throw new ArgumentException("Source span length must match the native array length.", nameof(values));
        }

        for (var index = 0; index < values.Length; index++)
        {
            SetAtCore((nuint)index, values[index]);
        }
    }

    public T[] ToArray()
    {
        var result = new T[Length];
        CopyTo(result, 0);
        return result;
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        ArgumentNullException.ThrowIfNull(array);

        if (arrayIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(arrayIndex));
        }

        if (array.Length - arrayIndex < Length)
        {
            throw new ArgumentException("Destination array is too small.", nameof(array));
        }

        for (var index = 0; index < Length; index++)
        {
            array[arrayIndex + index] = GetAtCore((nuint)index);
        }
    }

    public IEnumerator<T> GetEnumerator()
    {
        for (var index = 0; index < Length; index++)
        {
            yield return GetAtCore((nuint)index);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public int IndexOf(T item)
    {
        var comparer = EqualityComparer<T>.Default;
        for (var index = 0; index < Length; index++)
        {
            if (comparer.Equals(GetAtCore((nuint)index), item))
            {
                return index;
            }
        }

        return -1;
    }

    public bool Contains(T item) => IndexOf(item) >= 0;

    public void Add(T item) => throw new NotSupportedException("Fixed-size arrays cannot change length.");

    public void Clear() => throw new NotSupportedException("Fixed-size arrays cannot change length.");

    public void Insert(int index, T item) => throw new NotSupportedException("Fixed-size arrays cannot change length.");

    public bool Remove(T item) => throw new NotSupportedException("Fixed-size arrays cannot change length.");

    public void RemoveAt(int index) => throw new NotSupportedException("Fixed-size arrays cannot change length.");

    private void ValidateIndex(int index)
    {
        if ((uint)index >= (uint)Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }
}

public abstract class FixedArrayProxy<T> : IFixedArray<T>
{
    protected FixedArrayProxy(int length)
    {
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        Length = length;
    }

    public int Length { get; }

    public int Count => Length;

    public bool IsReadOnly => false;

    public T this[int index]
    {
        get
        {
            ValidateIndex(index);
            return GetAtCore((nuint)index);
        }
        set
        {
            ValidateIndex(index);
            SetAtCore((nuint)index, value);
        }
    }

    protected abstract T GetAtCore(nuint index);

    protected abstract void SetAtCore(nuint index, T value);

    public T[] ToArray()
    {
        var result = new T[Length];
        CopyTo(result, 0);
        return result;
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        ArgumentNullException.ThrowIfNull(array);

        if (arrayIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(arrayIndex));
        }

        if (array.Length - arrayIndex < Length)
        {
            throw new ArgumentException("Destination array is too small.", nameof(array));
        }

        for (var index = 0; index < Length; index++)
        {
            array[arrayIndex + index] = GetAtCore((nuint)index);
        }
    }

    public IEnumerator<T> GetEnumerator()
    {
        for (var index = 0; index < Length; index++)
        {
            yield return GetAtCore((nuint)index);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public int IndexOf(T item)
    {
        var comparer = EqualityComparer<T>.Default;
        for (var index = 0; index < Length; index++)
        {
            if (comparer.Equals(GetAtCore((nuint)index), item))
            {
                return index;
            }
        }

        return -1;
    }

    public bool Contains(T item) => IndexOf(item) >= 0;

    public void Add(T item) => throw new NotSupportedException("Fixed-size arrays cannot change length.");

    public void Clear() => throw new NotSupportedException("Fixed-size arrays cannot change length.");

    public void Insert(int index, T item) => throw new NotSupportedException("Fixed-size arrays cannot change length.");

    public bool Remove(T item) => throw new NotSupportedException("Fixed-size arrays cannot change length.");

    public void RemoveAt(int index) => throw new NotSupportedException("Fixed-size arrays cannot change length.");

    private void ValidateIndex(int index)
    {
        if ((uint)index >= (uint)Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }
}

public abstract class SharedArrayBase<T> : NativeObject
{
    protected SharedArrayBase(nint handle, int? length, bool ownsHandle)
        : base(handle, ownsHandle)
    {
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        Length = length;
    }

    public int? Length { get; }

    public bool HasKnownLength => Length.HasValue;

    public T this[nuint index]
    {
        get => GetAtCore(index);
        set => SetAtCore(index, value);
    }

    public T this[int index]
    {
        get
        {
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return GetAtCore((nuint)index);
        }
        set
        {
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            SetAtCore((nuint)index, value);
        }
    }

    protected abstract T GetAtCore(nuint index);

    protected abstract void SetAtCore(nuint index, T value);

    protected void Assign(ReadOnlySpan<T> values)
    {
        for (var index = 0; index < values.Length; index++)
        {
            SetAtCore((nuint)index, values[index]);
        }
    }

    public T[] ToArray(int length)
    {
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        var result = new T[length];
        for (var index = 0; index < length; index++)
        {
            result[index] = GetAtCore((nuint)index);
        }

        return result;
    }
}
