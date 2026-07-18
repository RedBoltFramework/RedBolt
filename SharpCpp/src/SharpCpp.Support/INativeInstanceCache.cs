namespace SharpCpp.Support;

/// <summary>
/// Contract for per-type instance caches that track managed NativeObject wrappers keyed by native handle,
/// preventing duplicate wrappers for the same native object.
/// </summary>
public interface INativeInstanceCache
{
    /// <summary>
    /// Attempts to retrieve an existing wrapper for the given native handle with compatible ownership.
    /// </summary>
    /// <param name="handle">The native object pointer.</param>
    /// <param name="ownsHandle">Whether the caller expects the wrapper to own the native handle.</param>
    /// <param name="instance">When this method returns, contains the existing wrapper if found and compatible; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if a compatible wrapper was found; otherwise, <see langword="false"/>.</returns>
    bool TryGetExisting(nint handle, bool ownsHandle, out NativeObject? instance);

    /// <summary>
    /// Registers a wrapper in the cache for its native handle.
    /// </summary>
    /// <param name="instance">The wrapper instance to register.</param>
    void Register(NativeObject instance);

    /// <summary>
    /// Removes a wrapper from the cache. Only removes if the reference matches the registering instance.
    /// </summary>
    /// <param name="instance">The wrapper instance to unregister.</param>
    void Unregister(NativeObject instance);
}
