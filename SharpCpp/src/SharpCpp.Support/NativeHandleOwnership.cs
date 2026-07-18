namespace SharpCpp.Support;

/// <summary>
/// Compile-time ownership policy used by generated native-handle marshallers.
/// </summary>
public interface INativeHandleOwnership
{
    /// <summary>
    /// Gets whether a wrapper created by this policy should own the native handle.
    /// </summary>
    static abstract bool OwnsHandle { get; }
}

/// <summary>
/// Marks a generated marshaller instantiation as borrowing the returned native handle.
/// </summary>
public readonly struct BorrowedNativeHandle : INativeHandleOwnership
{
    /// <inheritdoc />
    public static bool OwnsHandle => false;
}

/// <summary>
/// Marks a generated marshaller instantiation as owning the returned native handle.
/// </summary>
public readonly struct OwnedNativeHandle : INativeHandleOwnership
{
    /// <inheritdoc />
    public static bool OwnsHandle => true;
}
