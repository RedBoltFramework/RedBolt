using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace SharpCpp.Support;

/// <summary>
/// Thread-safe default implementation of <see cref="INativeInstanceCache"/> using <see cref="ConcurrentDictionary{TKey, TValue}"/>
/// with <see cref="WeakReference{T}"/> values so the cache never prevents garbage collection.
/// </summary>
public sealed class DefaultNativeInstanceCache : INativeInstanceCache
{
    private readonly ConcurrentDictionary<nint, WeakReference<NativeObject>> _instances = new();

    /// <summary>
    /// Attempts to retrieve an existing wrapper for the given native handle.
    /// Automatically removes stale entries (collected or closed wrappers) and checks ownership compatibility.
    /// Thread-safe and lock-free via <see cref="ConcurrentDictionary{TKey, TValue}"/>.
    /// </summary>
    /// <param name="handle">The native object pointer.</param>
    /// <param name="ownsHandle">Whether the caller expects the wrapper to own the native handle.</param>
    /// <param name="instance">When this method returns, contains the existing wrapper if found and compatible; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if a compatible wrapper was found; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetExisting(nint handle, bool ownsHandle, out NativeObject? instance)
    {
        if (handle == IntPtr.Zero || !_instances.TryGetValue(handle, out var reference))
        {
            instance = null;
            return false;
        }

        if (reference.TryGetTarget(out instance) &&
            !instance.IsClosed &&
            (!ownsHandle || instance.OwnsNativeHandle || instance.TryPromoteToOwningHandle()))
        {
            return true;
        }

        return RemoveStaleEntry(handle, out instance);
    }

    /// <summary>
    /// Registers a wrapper in the cache for its native handle.
    /// If an existing owning wrapper is already registered and the new instance does not own the handle,
    /// the registration is silently skipped to preserve the existing owning reference.
    /// Stale or invalid instances are ignored.
    /// Thread-safe and lock-free via <see cref="ConcurrentDictionary{TKey, TValue}"/>.
    /// </summary>
    /// <param name="instance">The wrapper instance to register.</param>
    public void Register(NativeObject instance)
    {
        if (instance.IsClosed || instance.IsInvalid)
        {
            return;
        }

        var handle = instance.DangerousGetHandle();

        if (_instances.TryGetValue(handle, out var existingReference) &&
            existingReference.TryGetTarget(out var existing) &&
            !existing.IsClosed &&
            !existing.IsInvalid &&
            existing.OwnsNativeHandle &&
            !instance.OwnsNativeHandle &&
            !ReferenceEquals(existing, instance))
        {
            return;
        }

        _instances[handle] = new WeakReference<NativeObject>(instance);
    }

    /// <summary>
    /// Removes a wrapper from the cache. Only removes if the reference matches the registering instance,
    /// or if the stored reference has been collected (stale entry cleanup).
    /// Closed or invalid instances are ignored.
    /// Thread-safe and lock-free via <see cref="ConcurrentDictionary{TKey, TValue}"/>.
    /// </summary>
    /// <param name="instance">The wrapper instance to unregister.</param>
    public void Unregister(NativeObject instance)
    {
        if (instance.IsClosed || instance.IsInvalid)
        {
            return;
        }

        var handle = instance.DangerousGetHandle();

        if (!_instances.TryGetValue(handle, out var reference))
        {
            return;
        }

        if (!reference.TryGetTarget(out var existing) || ReferenceEquals(existing, instance))
        {
            _instances.TryRemove(handle, out _);
        }
    }

    /// <summary>
    /// Removes a stale entry from the cache (collected wrapper or ownership mismatch).
    /// Marked NoInlining to keep TryGetExisting's hot path small.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool RemoveStaleEntry(nint handle, out NativeObject? instance)
    {
        _instances.TryRemove(handle, out _);
        instance = null;
        return false;
    }
}
