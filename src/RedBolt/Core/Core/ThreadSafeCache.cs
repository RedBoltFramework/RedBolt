// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Core;

using System.Runtime.CompilerServices;
using System.Threading;

/// <summary>
/// Thread-safe cache that holds an object.
/// It's safe to:
/// - Invalidate cached object from multiple threads;
/// - Restore cached object from multiple threads, as long as all threads assign the same value.
/// It's unsafe to both invalidate and restore cached object from multiple threads simultaneously.
/// If different threads assign different values on Restore, cache will keep first provided value.
/// </summary>
public class ThreadSafeCache<T>
{
    /// <summary>
    /// Invalidate cached object.
    /// </summary>
    public void Invalidate()
    {
        Volatile.Write(ref _dirty, 1);
    }

    /// <summary>
    /// Return whether the object is invalid and has to be restored.
    /// </summary>
    public bool IsInvalidated()
    {
        return Volatile.Read(ref _dirty) != 0;
    }

    /// <summary>
    /// Restore cached object. This call may be ignored if cache is already restored.
    /// </summary>
    public void Restore(T obj)
    {
        lock (_mutex)
        {
            if (Volatile.Read(ref _dirty) != 0)
            {
                _object.Value = obj;
                Volatile.Write(ref _dirty, 0);
            }
        }
    }

    /// <summary>
    /// Return object value. Intentionally unchecked, caller must ensure that cache is valid.
    /// </summary>
    public ref readonly T? Get()
    {
        return ref _object.Value;
    }

    internal StrongBox<T?> GetStorage() => _object;

    private int _dirty = 1;
    private readonly object _mutex = new();
    private readonly StrongBox<T?> _object = new();
}
