using System;
using System.Collections;
using System.Collections.Generic;

namespace SharpCpp.Support;

/// <summary>
/// Base class for generated wrappers of native <c>std::vector&lt;T&gt;</c>.
/// </summary>
/// <typeparam name="T">The type of elements in the vector.</typeparam>
public abstract class VectorBase<T> : IndexableCollectionBase<T>
{
    /// <summary>
    /// Initializes a new vector wrapper for the given native handle.
    /// </summary>
    /// <param name="handle">Native handle to the std::vector.</param>
    /// <param name="ownsHandle"><see langword="true"/> if this instance owns the native handle; otherwise <see langword="false"/>.</param>
    protected VectorBase(nint handle, bool ownsHandle)
        : base(handle, ownsHandle)
    {
    }
}

/// <summary>
/// Base class for generated wrappers of native <c>std::list&lt;T&gt;</c>.
/// </summary>
/// <typeparam name="T">The type of elements in the list.</typeparam>
public abstract class ListBase<T> : IndexableCollectionBase<T>
{
    /// <summary>
    /// Initializes a new list wrapper for the given native handle.
    /// </summary>
    /// <param name="handle">Native handle to the std::list.</param>
    /// <param name="ownsHandle"><see langword="true"/> if this instance owns the native handle; otherwise <see langword="false"/>.</param>
    protected ListBase(nint handle, bool ownsHandle)
        : base(handle, ownsHandle)
    {
    }
}

/// <summary>
/// Base class for generated wrappers of native <c>std::map&lt;TKey, TValue&gt;</c> (ordered map).
/// </summary>
/// <typeparam name="TKey">The type of keys in the map.</typeparam>
/// <typeparam name="TValue">The type of values in the map.</typeparam>
public abstract class MapBase<TKey, TValue> : DictionaryBase<TKey, TValue>
    where TKey : notnull
{
    /// <summary>
    /// Initializes a new ordered-map wrapper for the given native handle.
    /// </summary>
    /// <param name="handle">Native handle to the std::map.</param>
    /// <param name="ownsHandle"><see langword="true"/> if this instance owns the native handle; otherwise <see langword="false"/>.</param>
    protected MapBase(nint handle, bool ownsHandle)
        : base(handle, ownsHandle)
    {
    }
}

/// <summary>
/// Base class for generated wrappers of native <c>std::unordered_map&lt;TKey, TValue&gt;</c>.
/// </summary>
/// <typeparam name="TKey">The type of keys in the map.</typeparam>
/// <typeparam name="TValue">The type of values in the map.</typeparam>
public abstract class UnorderedMapBase<TKey, TValue> : DictionaryBase<TKey, TValue>
    where TKey : notnull
{
    /// <summary>
    /// Initializes a new unordered-map wrapper for the given native handle.
    /// </summary>
    /// <param name="handle">Native handle to the std::unordered_map.</param>
    /// <param name="ownsHandle"><see langword="true"/> if this instance owns the native handle; otherwise <see langword="false"/>.</param>
    protected UnorderedMapBase(nint handle, bool ownsHandle)
        : base(handle, ownsHandle)
    {
    }
}

/// <summary>
/// Sequence collection base for generated wrappers of indexable native containers
/// (<c>std::vector</c>, <c>std::list</c>).
/// Extends <see cref="NativeObject"/> and implements <see cref="IList{T}"/>.
/// </summary>
/// <typeparam name="T">The type of elements in the collection.</typeparam>
public abstract class IndexableCollectionBase<T> : NativeObject, IList<T>
{
    /// <summary>
    /// Initializes a new indexable-collection wrapper for the given native handle.
    /// </summary>
    /// <param name="handle">Native handle to the underlying container.</param>
    /// <param name="ownsHandle"><see langword="true"/> if this instance owns the native handle; otherwise <see langword="false"/>.</param>
    protected IndexableCollectionBase(nint handle, bool ownsHandle)
        : base(handle, ownsHandle)
    {
    }

    /// <summary>
    /// When overridden in a derived class, gets the number of elements in the native container.
    /// </summary>
    /// <returns>The element count.</returns>
    protected abstract ulong GetCountCore();

    /// <summary>
    /// When overridden in a derived class, clears all elements from the native container.
    /// </summary>
    protected abstract void ClearCore();

    /// <summary>
    /// When overridden in a derived class, gets the element at the specified index from the native container.
    /// </summary>
    /// <param name="index">The zero-based index of the element.</param>
    /// <returns>The element at the specified index.</returns>
    protected abstract T GetAtCore(ulong index);

    /// <summary>
    /// When overridden in a derived class, sets the element at the specified index in the native container.
    /// </summary>
    /// <param name="index">The zero-based index of the element.</param>
    /// <param name="value">The new value.</param>
    protected abstract void SetAtCore(ulong index, T value);

    /// <summary>
    /// When overridden in a derived class, appends an element to the native container.
    /// </summary>
    /// <param name="value">The element to add.</param>
    protected abstract void AddCore(T value);

    /// <summary>
    /// When overridden in a derived class, inserts an element at the specified index in the native container.
    /// </summary>
    /// <param name="index">The zero-based index at which to insert.</param>
    /// <param name="value">The element to insert.</param>
    protected abstract void InsertAtCore(ulong index, T value);

    /// <summary>
    /// When overridden in a derived class, removes the element at the specified index from the native container.
    /// </summary>
    /// <param name="index">The zero-based index of the element to remove.</param>
    protected abstract void RemoveAtCore(ulong index);

    /// <summary>
    /// Gets the number of elements in the collection.
    /// </summary>
    public int Count => checked((int)GetCountCore());

    /// <summary>
    /// Gets a value indicating whether the collection is read-only.
    /// </summary>
    public bool IsReadOnly => false;

    /// <summary>
    /// Gets or sets the element at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the element.</param>
    /// <returns>The element at the specified index.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not a valid index in the collection.</exception>
    public T this[int index]
    {
        get
        {
            ValidateExistingIndex(index);
            return GetAtCore((ulong)index);
        }
        set
        {
            ValidateExistingIndex(index);
            SetAtCore((ulong)index, value);
        }
    }

    /// <summary>
    /// Adds an element to the end of the collection.
    /// Delegates to the abstract <see cref="AddCore"/> method.
    /// </summary>
    /// <param name="item">The element to add.</param>
    public void Add(T item)
    {
        AddCore(item);
    }

    /// <summary>
    /// Removes all elements from the collection.
    /// Delegates to the abstract <see cref="ClearCore"/> method.
    /// </summary>
    public void Clear()
    {
        ClearCore();
    }

    /// <summary>
    /// Determines whether the collection contains a specific value.
    /// </summary>
    /// <param name="item">The element to locate.</param>
    /// <returns><see langword="true"/> if the element is found; otherwise <see langword="false"/>.</returns>
    public bool Contains(T item)
    {
        return IndexOf(item) >= 0;
    }

    /// <summary>
    /// Copies the elements of the collection to an array, starting at the specified index.
    /// </summary>
    /// <param name="array">The destination array.</param>
    /// <param name="arrayIndex">The zero-based index in <paramref name="array"/> at which copying begins.</param>
    /// <exception cref="ArgumentNullException"><paramref name="array"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="arrayIndex"/> is less than 0.</exception>
    /// <exception cref="ArgumentException">The destination array is too small to hold the copied elements.</exception>
    public void CopyTo(T[] array, int arrayIndex)
    {
        ArgumentNullException.ThrowIfNull(array);

        if (arrayIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(arrayIndex));
        }

        if (array.Length - arrayIndex < Count)
        {
            throw new ArgumentException("Destination array is too small.", nameof(array));
        }

        for (var index = 0; index < Count; index++)
        {
            array[arrayIndex + index] = GetAtCore((ulong)index);
        }
    }

    /// <summary>
    /// Returns an enumerator that iterates through the collection.
    /// </summary>
    /// <returns>An enumerator for the collection.</returns>
    public IEnumerator<T> GetEnumerator()
    {
        for (var index = 0; index < Count; index++)
        {
            yield return GetAtCore((ulong)index);
        }
    }

    /// <summary>
    /// Determines the index of a specific element in the collection.
    /// </summary>
    /// <param name="item">The element to locate.</param>
    /// <returns>The zero-based index of the element if found; otherwise -1.</returns>
    public int IndexOf(T item)
    {
        var comparer = EqualityComparer<T>.Default;
        for (var index = 0; index < Count; index++)
        {
            if (comparer.Equals(GetAtCore((ulong)index), item))
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    /// Inserts an element at the specified index.
    /// Delegates to the abstract <see cref="InsertAtCore"/> method.
    /// </summary>
    /// <param name="index">The zero-based index at which to insert.</param>
    /// <param name="item">The element to insert.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not a valid insertion index (must be between 0 and <see cref="Count"/> inclusive).</exception>
    public void Insert(int index, T item)
    {
        ValidateInsertIndex(index);
        InsertAtCore((ulong)index, item);
    }

    /// <summary>
    /// Removes the first occurrence of a specific element from the collection.
    /// Delegates to the abstract <see cref="RemoveAtCore"/> method.
    /// </summary>
    /// <param name="item">The element to remove.</param>
    /// <returns><see langword="true"/> if the element was successfully removed; otherwise <see langword="false"/>.</returns>
    public bool Remove(T item)
    {
        var index = IndexOf(item);
        if (index < 0)
        {
            return false;
        }

        RemoveAtCore((ulong)index);
        return true;
    }

    /// <summary>
    /// Removes the element at the specified index.
    /// Delegates to the abstract <see cref="RemoveAtCore"/> method.
    /// </summary>
    /// <param name="index">The zero-based index of the element to remove.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not a valid index in the collection.</exception>
    public void RemoveAt(int index)
    {
        ValidateExistingIndex(index);
        RemoveAtCore((ulong)index);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <summary>
    /// Validates the index for read/write access.
    /// </summary>
    /// <param name="index">The zero-based index to validate.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the valid range.</exception>
    protected void ValidateExistingIndex(int index)
    {
        if ((uint)index >= (uint)Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    /// <summary>
    /// Validates the index for insertion.
    /// </summary>
    /// <param name="index">The zero-based index to validate.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the valid insertion range (0 to <see cref="Count"/> inclusive).</exception>
    protected void ValidateInsertIndex(int index)
    {
        if (index < 0 || index > Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }
}

/// <summary>
/// Associative collection base for generated wrappers of native map-like containers.
/// Extends <see cref="NativeObject"/> and implements <see cref="IDictionary{TKey, TValue}"/>
/// and <see cref="IReadOnlyDictionary{TKey, TValue}"/>.
/// </summary>
/// <typeparam name="TKey">The type of keys in the dictionary.</typeparam>
/// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
public abstract class DictionaryBase<TKey, TValue> : NativeObject, IDictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>
    where TKey : notnull
{
    /// <summary>
    /// Initializes a new dictionary wrapper for the given native handle.
    /// </summary>
    /// <param name="handle">Native handle to the underlying map container.</param>
    /// <param name="ownsHandle"><see langword="true"/> if this instance owns the native handle; otherwise <see langword="false"/>.</param>
    protected DictionaryBase(nint handle, bool ownsHandle)
        : base(handle, ownsHandle)
    {
    }

    /// <summary>
    /// When overridden in a derived class, gets the number of key-value pairs in the native map.
    /// </summary>
    /// <returns>The element count.</returns>
    protected abstract ulong GetCountCore();

    /// <summary>
    /// When overridden in a derived class, clears all key-value pairs from the native map.
    /// </summary>
    protected abstract void ClearCore();

    /// <summary>
    /// When overridden in a derived class, determines whether the native map contains the specified key.
    /// </summary>
    /// <param name="key">The key to locate.</param>
    /// <returns><see langword="true"/> if the key is found; otherwise <see langword="false"/>.</returns>
    protected abstract bool ContainsKeyCore(TKey key);

    /// <summary>
    /// When overridden in a derived class, gets the value associated with the specified key from the native map.
    /// </summary>
    /// <param name="key">The key whose value to get.</param>
    /// <returns>The value associated with the specified key.</returns>
    protected abstract TValue GetValueCore(TKey key);

    /// <summary>
    /// When overridden in a derived class, sets the value associated with the specified key in the native map.
    /// </summary>
    /// <param name="key">The key whose value to set.</param>
    /// <param name="value">The new value.</param>
    protected abstract void SetValueCore(TKey key, TValue value);

    /// <summary>
    /// When overridden in a derived class, adds a new key-value pair to the native map.
    /// </summary>
    /// <param name="key">The key to add.</param>
    /// <param name="value">The value to add.</param>
    /// <returns><see langword="true"/> if the pair was added; <see langword="false"/> if the key already exists.</returns>
    protected abstract bool AddCore(TKey key, TValue value);

    /// <summary>
    /// When overridden in a derived class, removes the key-value pair with the specified key from the native map.
    /// </summary>
    /// <param name="key">The key to remove.</param>
    /// <returns><see langword="true"/> if the key was found and removed; otherwise <see langword="false"/>.</returns>
    protected abstract bool RemoveCore(TKey key);

    /// <summary>
    /// When overridden in a derived class, gets the key at the specified index from the native map's ordered iteration.
    /// </summary>
    /// <param name="index">The zero-based index of the key.</param>
    /// <returns>The key at the specified index.</returns>
    protected abstract TKey GetKeyAtCore(ulong index);

    /// <summary>
    /// When overridden in a derived class, gets the value at the specified index from the native map's ordered iteration.
    /// </summary>
    /// <param name="index">The zero-based index of the value.</param>
    /// <returns>The value at the specified index.</returns>
    protected abstract TValue GetValueAtCore(ulong index);

    /// <summary>
    /// Gets the number of key-value pairs in the dictionary.
    /// </summary>
    public int Count => checked((int)GetCountCore());

    /// <summary>
    /// Gets a value indicating whether the dictionary is read-only.
    /// </summary>
    public bool IsReadOnly => false;

    /// <summary>
    /// Gets or sets the value associated with the specified key.
    /// </summary>
    /// <param name="key">The key of the value to get or set.</param>
    /// <returns>The value associated with the specified key.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
    /// <exception cref="KeyNotFoundException">The property is retrieved and <paramref name="key"/> is not found.</exception>
    public TValue this[TKey key]
    {
        get
        {
            ArgumentNullException.ThrowIfNull(key);
            if (!ContainsKeyCore(key))
            {
                throw new KeyNotFoundException();
            }

            return GetValueCore(key);
        }
        set
        {
            ArgumentNullException.ThrowIfNull(key);
            SetValueCore(key, value);
        }
    }

    /// <summary>
    /// Gets a collection containing the keys of the dictionary.
    /// </summary>
    public ICollection<TKey> Keys => MaterializeKeys();

    /// <summary>
    /// Gets a collection containing the values of the dictionary.
    /// </summary>
    public ICollection<TValue> Values => MaterializeValues();

    IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => MaterializeKeys();

    IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => MaterializeValues();

    /// <summary>
    /// Adds the specified key and value to the dictionary.
    /// Delegates to the abstract <see cref="AddCore"/> method.
    /// </summary>
    /// <param name="key">The key to add.</param>
    /// <param name="value">The value to add.</param>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">An element with the same key already exists.</exception>
    public void Add(TKey key, TValue value)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (!AddCore(key, value))
        {
            throw new ArgumentException("An element with the same key already exists.", nameof(key));
        }
    }

    /// <summary>
    /// Determines whether the dictionary contains the specified key.
    /// Delegates to the abstract <see cref="ContainsKeyCore"/> method.
    /// </summary>
    /// <param name="key">The key to locate.</param>
    /// <returns><see langword="true"/> if the dictionary contains the key; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
    public bool ContainsKey(TKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return ContainsKeyCore(key);
    }

    /// <summary>
    /// Removes the key-value pair with the specified key from the dictionary.
    /// Delegates to the abstract <see cref="RemoveCore"/> method.
    /// </summary>
    /// <param name="key">The key to remove.</param>
    /// <returns><see langword="true"/> if the element was successfully removed; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
    public bool Remove(TKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return RemoveCore(key);
    }

    /// <summary>
    /// Attempts to get the value associated with the specified key.
    /// </summary>
    /// <param name="key">The key whose value to get.</param>
    /// <param name="value">When this method returns, contains the value associated with the specified key
    /// if found; otherwise the default value.</param>
    /// <returns><see langword="true"/> if the dictionary contains the key; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
    public bool TryGetValue(TKey key, out TValue value)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (!ContainsKeyCore(key))
        {
            value = default!;
            return false;
        }

        value = GetValueCore(key);
        return true;
    }

    /// <summary>
    /// Adds a key-value pair to the dictionary.
    /// </summary>
    /// <param name="item">The key-value pair to add.</param>
    public void Add(KeyValuePair<TKey, TValue> item)
    {
        Add(item.Key, item.Value);
    }

    /// <summary>
    /// Removes all key-value pairs from the dictionary.
    /// Delegates to the abstract <see cref="ClearCore"/> method.
    /// </summary>
    public void Clear()
    {
        ClearCore();
    }

    /// <summary>
    /// Determines whether the dictionary contains the specified key-value pair.
    /// </summary>
    /// <param name="item">The key-value pair to locate.</param>
    /// <returns><see langword="true"/> if the key-value pair is found; otherwise <see langword="false"/>.</returns>
    public bool Contains(KeyValuePair<TKey, TValue> item)
    {
        return TryGetValue(item.Key, out var value) && EqualityComparer<TValue>.Default.Equals(value, item.Value);
    }

    /// <summary>
    /// Copies the key-value pairs of the dictionary to an array, starting at the specified index.
    /// </summary>
    /// <param name="array">The destination array.</param>
    /// <param name="arrayIndex">The zero-based index in <paramref name="array"/> at which copying begins.</param>
    /// <exception cref="ArgumentNullException"><paramref name="array"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="arrayIndex"/> is less than 0.</exception>
    /// <exception cref="ArgumentException">The destination array is too small to hold the copied elements.</exception>
    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        ArgumentNullException.ThrowIfNull(array);

        if (arrayIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(arrayIndex));
        }

        if (array.Length - arrayIndex < Count)
        {
            throw new ArgumentException("Destination array is too small.", nameof(array));
        }

        for (var index = 0; index < Count; index++)
        {
            array[arrayIndex + index] = new KeyValuePair<TKey, TValue>(GetKeyAtCore((ulong)index), GetValueAtCore((ulong)index));
        }
    }

    /// <summary>
    /// Removes the specified key-value pair from the dictionary.
    /// </summary>
    /// <param name="item">The key-value pair to remove.</param>
    /// <returns><see langword="true"/> if the pair was successfully removed; otherwise <see langword="false"/>.</returns>
    public bool Remove(KeyValuePair<TKey, TValue> item)
    {
        return Contains(item) && RemoveCore(item.Key);
    }

    /// <summary>
    /// Returns an enumerator that iterates through the dictionary.
    /// </summary>
    /// <returns>An enumerator for the dictionary.</returns>
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        for (var index = 0; index < Count; index++)
        {
            yield return new KeyValuePair<TKey, TValue>(GetKeyAtCore((ulong)index), GetValueAtCore((ulong)index));
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    private List<TKey> MaterializeKeys()
    {
        var keys = new List<TKey>(Count);
        for (var index = 0; index < Count; index++)
        {
            keys.Add(GetKeyAtCore((ulong)index));
        }

        return keys;
    }

    private List<TValue> MaterializeValues()
    {
        var values = new List<TValue>(Count);
        for (var index = 0; index < Count; index++)
        {
            values.Add(GetValueAtCore((ulong)index));
        }

        return values;
    }
}
