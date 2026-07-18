using System.Collections.Concurrent;

namespace SharpCpp.Support;

/// <summary>
/// Static type registry mapping native dynamic type IDs to managed wrapper factories,
/// enabling polymorphic wrapper creation when the exact derived type is only known at runtime.
/// </summary>
public abstract class NativeObjectRegistry
{
    private readonly record struct RegisteredType(
        Type ExpectedType,
        Func<nint, bool, NativeObject> CreateFactory,
        INativeInstanceCache InstanceCache);

    private static readonly ConcurrentDictionary<nuint, RegisteredType> s_types = new();

    /// <summary>
    /// Registers the given wrapper type by reading its <see cref="INativeObjectRegistration{T}"/> static abstract members.
    /// </summary>
    /// <typeparam name="T">The wrapper type to register. Must implement <see cref="INativeObjectRegistration{T}"/>.</typeparam>
    /// <exception cref="InvalidOperationException">Thrown when <c>T.RegistrationCreateFactory</c> is <see langword="null"/>.</exception>
    public static void RegisterType<T>()
        where T : NativeObject, INativeObjectRegistration<T>
    {
        if (!T.RegistrationIsPolymorphic)
        {
            return;
        }

        var createFactory = T.RegistrationCreateFactory
            ?? throw new InvalidOperationException($"No managed wrapper factory is registered for '{typeof(T).FullName}'.");

        RegisterType(T.RegistrationDynamicTypeId, T.RegistrationExpectedType, createFactory, T.RegistrationInstanceCache);
    }

    public static void RegisterInstance<T>(T instance)
        where T : NativeObject, INativeObjectRegistration<T>
    {
        ArgumentNullException.ThrowIfNull(instance);

        instance.RegisterInInstanceCache(T.RegistrationInstanceCache);
    }

    /// <summary>
    /// Registers a factory for the given native dynamic type ID.
    /// </summary>
    /// <param name="dynamicTypeId">The C++ runtime type identifier to map.</param>
    /// <param name="expectedType">The expected CLR wrapper <see cref="Type"/>.</param>
    /// <param name="createFactory">A factory delegate that creates a managed wrapper for a given native handle and ownership flag.</param>
    /// <param name="instanceCache">The per-type instance cache for tracking live wrappers.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="expectedType"/>, <paramref name="createFactory"/>, or <paramref name="instanceCache"/> is <see langword="null"/>.</exception>
    public static void RegisterType(
        nuint dynamicTypeId,
        Type expectedType,
        Func<nint, bool, NativeObject> createFactory,
        INativeInstanceCache instanceCache)
    {
        ArgumentNullException.ThrowIfNull(expectedType);
        ArgumentNullException.ThrowIfNull(createFactory);
        ArgumentNullException.ThrowIfNull(instanceCache);

        s_types[dynamicTypeId] = new RegisteredType(expectedType, createFactory, instanceCache);
    }

    /// <summary>
    /// Primary entry point for generated P/Invoke wrappers. Resolves the most derived registered wrapper type
    /// for the given native handle via cache lookup, native metadata query, and registry resolution.
    /// </summary>
    /// <typeparam name="T">The expected wrapper type. Must implement <see cref="INativeObjectRegistration{T}"/>.</typeparam>
    /// <param name="handle">The native object pointer.</param>
    /// <param name="ownsHandle">Whether the wrapper should own the native handle.</param>
    /// <returns>A managed wrapper instance for the native object.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no factory is available for the resolved type.</exception>
    public static NativeObject Create<T>(
        nint handle,
        bool ownsHandle)
        where T : NativeObject, INativeObjectRegistration<T>
    {
        if (handle == IntPtr.Zero)
        {
            return null!;
        }

        var metadata = T.RegistrationIsPolymorphic
            ? T.RegistrationGetMetadata(handle).Normalize(handle)
            : new NativeObjectMetadata(handle, T.RegistrationDynamicTypeId);
        var instanceCache = T.RegistrationInstanceCache;
        var factory = T.RegistrationCreateFactory;
        if (T.RegistrationIsPolymorphic && s_types.TryGetValue(metadata.DynamicTypeId, out var registeredType))
        {
            instanceCache = registeredType.InstanceCache;
            factory = registeredType.CreateFactory;
        }

        handle = metadata.Handle;

        if (instanceCache.TryGetExisting(handle, ownsHandle, out var existing) && existing is not null)
        {
            return existing;
        }

        if (factory is null)
        {
            throw new InvalidOperationException($"No managed wrapper factory is available for '{typeof(T).FullName}'.");
        }

        var instance = factory(handle, ownsHandle);
        instance.RegisterInInstanceCache(instanceCache);
        return instance;
    }
}
