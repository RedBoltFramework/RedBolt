namespace RedBolt;

/// <summary>
/// Utility to assign unique non-zero IDs to objects. Thread-safe.
/// </summary>
public class IndexAllocator
{
    /// <summary>
    /// Return upper bound of allocated indices.
    /// </summary>
    public uint GetNextFreeIndex() => Volatile.Read(ref _nextIndex);

    /// <summary>
    /// Return number of currently allocated indices.
    /// </summary>
    public uint GetSize()
    {
        lock (_mutex)
            return GetNextFreeIndex() - (uint)_unusedIndices.Count;
    }

    /// <summary>
    /// Allocate index.
    /// </summary>
    public uint Allocate()
    {
        lock (_mutex)
        {
            uint index;
            if (_unusedIndices.Count == 0)
            {
                index = GetNextFreeIndex();
                _nextIndex++;
            }
            else
            {
                var lastIndex = _unusedIndices.Count - 1;
                index = _unusedIndices[lastIndex];
                _unusedIndices.RemoveAt(lastIndex);
            }

            return index;
        }
    }

    /// <summary>
    /// Release index. Index should be previously returned from Allocate and not released yet.
    /// </summary>
    public void Release(uint index)
    {
        lock (_mutex)
        {
            if (index + 1 == GetNextFreeIndex())
                _nextIndex--;
            else
                _unusedIndices.Add(index);
        }
    }

    /// <summary>
    /// Shrink collection to minimum possible size preserving currently allocated indices.
    /// </summary>
    public void Shrink()
    {
        lock (_mutex)
        {
            _unusedIndices.Sort();
            while (_unusedIndices.Count > 0 && _unusedIndices[^1] + 1 == GetNextFreeIndex())
            {
                _unusedIndices.RemoveAt(_unusedIndices.Count - 1);
                _nextIndex--;
            }
            _unusedIndices.TrimExcess();
        }
    }

    /// <summary>
    /// Reset to default state.
    /// </summary>
    public void Clear()
    {
        lock (_mutex)
        {
            _nextIndex = 1;
            _unusedIndices.Clear();
        }
    }

    /// <summary>
    /// Mutex that protects list and index.
    /// </summary>
    private readonly object _mutex = new();

    /// <summary>
    /// Next unused index.
    /// </summary>
    private uint _nextIndex = 1;

    /// <summary>
    /// Unused indices.
    /// </summary>
    private readonly List<uint> _unusedIndices = [];
}

/// <summary>
/// Family of unique indices for template type.
/// </summary>
public class IDFamily<T> : IDisposable
{
    /// <summary>
    /// Construct.
    /// </summary>
    public IDFamily()
    {
        AcquireObjectID();
    }

    /// <summary>
    /// Destruct.
    /// </summary>
    public void Dispose()
    {
        ReleaseObjectID();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Return unique object ID or 0 if not assigned.
    /// </summary>
    public uint GetObjectID() => _objectId;

    /// <summary>
    /// Return upper bound of all used object IDs within family.
    /// </summary>
    public static uint GetNextFreeObjectID() => _indexAllocator.GetNextFreeIndex();

    /// <summary>
    /// Acquire unique object ID. Ignored if unique ID is already acquired.
    /// </summary>
    public void AcquireObjectID()
    {
        if (_objectId == 0)
            _objectId = _indexAllocator.Allocate();
    }

    /// <summary>
    /// Release unique object ID. Ignored if unique ID is already released.
    /// </summary>
    public void ReleaseObjectID()
    {
        if (_objectId != 0)
        {
            _indexAllocator.Release(_objectId);
            _objectId = 0;
        }
    }

    /// <summary>
    /// Shared allocator for this family.
    /// </summary>
    private static readonly IndexAllocator _indexAllocator = new();

    /// <summary>
    /// Unique object ID.
    /// </summary>
    private uint _objectId;
}
