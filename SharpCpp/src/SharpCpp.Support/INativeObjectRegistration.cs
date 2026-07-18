namespace SharpCpp.Support;

/// <summary>
/// Static abstract interface implemented by generated wrapper types to provide per-type metadata
/// (dynamic type ID, expected CLR type, instance cache, factory, native metadata query) without reflection.
/// </summary>
/// <typeparam name="TSelf">The implementing NativeObject subclass.</typeparam>
public interface INativeObjectRegistration<TSelf>
    where TSelf : NativeObject
{
    /// <summary>
    /// Gets whether this wrapper needs native runtime metadata to resolve derived wrapper types.
    /// </summary>
    static abstract bool RegistrationIsPolymorphic { get; }

    /// <summary>
    /// Gets the C++ runtime type identifier (dynamic type ID) for this wrapper type.
    /// Used by <see cref="NativeObjectRegistry.Create{T}"/> to resolve the most derived registered wrapper.
    /// </summary>
    static abstract nuint RegistrationDynamicTypeId { get; }

    /// <summary>
    /// Gets the expected CLR <see cref="Type"/> for this wrapper type.
    /// Used for validation and registration metadata.
    /// </summary>
    static abstract Type RegistrationExpectedType { get; }

    /// <summary>
    /// Gets the per-type <see cref="INativeInstanceCache"/> that tracks live wrapper instances
    /// keyed by native handle, preventing duplicate wrappers for the same native object.
    /// </summary>
    static abstract INativeInstanceCache RegistrationInstanceCache { get; }

    /// <summary>
    /// Gets a factory delegate that creates a managed wrapper for a given native handle and ownership flag.
    /// May be <see langword="null"/> if the type cannot be instantiated as a concrete wrapper
    /// (e.g. abstract base types).
    /// </summary>
    static abstract Func<nint, bool, NativeObject>? RegistrationCreateFactory { get; }

    /// <summary>
    /// Queries the native runtime for metadata about the object at the given handle, returning
    /// a <see cref="NativeObjectMetadata"/> value that includes the potentially adjusted handle
    /// and the dynamic type identifier.
    /// </summary>
    /// <param name="handle">The native object pointer to query.</param>
    /// <returns>A <see cref="NativeObjectMetadata"/> struct containing the handle and dynamic type ID.</returns>
    static abstract NativeObjectMetadata RegistrationGetMetadata(nint handle);
}
