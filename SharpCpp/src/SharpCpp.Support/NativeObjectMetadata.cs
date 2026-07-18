using System.Runtime.InteropServices;

namespace SharpCpp.Support;

/// <summary>
/// Blittable value type that flows across the P/Invoke boundary carrying a native object pointer
/// and its C++ runtime type identifier.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct NativeObjectMetadata
{
    /// <summary>
    /// Initializes a new instance of <see cref="NativeObjectMetadata"/> with the given handle and dynamic type ID.
    /// </summary>
    /// <param name="handle">The native object pointer.</param>
    /// <param name="dynamicTypeId">The C++ runtime type identifier (e.g. rbfx's type hash).</param>
    public NativeObjectMetadata(nint handle, nuint dynamicTypeId)
    {
        Handle = handle;
        DynamicTypeId = dynamicTypeId;
    }

    /// <summary>
    /// The native object pointer.
    /// </summary>
    public nint Handle { get; }

    /// <summary>
    /// The C++ runtime type identifier (e.g. rbfx's type hash).
    /// </summary>
    public nuint DynamicTypeId { get; }

    /// <summary>
    /// Returns this instance if <see cref="Handle"/> is non-zero; otherwise creates a new instance
    /// with the given fallback handle.
    /// </summary>
    /// <param name="fallbackHandle">The handle to use when <see cref="Handle"/> is zero.</param>
    /// <returns>This instance if <see cref="Handle"/> is non-zero; otherwise a new instance with <paramref name="fallbackHandle"/>.</returns>
    public NativeObjectMetadata Normalize(nint fallbackHandle)
    {
        return Handle != IntPtr.Zero
            ? this
            : new NativeObjectMetadata(fallbackHandle, DynamicTypeId);
    }
}
