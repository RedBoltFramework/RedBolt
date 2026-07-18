using System.Reflection;
using System.Runtime.InteropServices;

namespace SharpCpp.Support;

/// <summary>
/// Root of the managed binding model. Provides deterministic native resource lifetime, dual-path disposal,
/// instance cache integration, and static helpers for extracting handles from wrappers.
/// </summary>
public abstract class NativeObject : SafeHandle
{
    private readonly object _ownershipSync = new();

    /// <summary>
    /// Initializes a new instance wrapping the given native handle.
    /// </summary>
    /// <param name="nativeHandle">The native pointer to wrap.</param>
    /// <param name="ownsHandle">
    /// <see langword="true"/> if this wrapper owns the native object's lifetime and should release it on dispose;
    /// <see langword="false"/> to borrow the handle without taking ownership.
    /// </param>
    protected NativeObject(nint nativeHandle, bool ownsHandle)
        : base(IntPtr.Zero, ownsHandle: true)
    {
        _ownsNativeHandle = ownsHandle;
        SetHandle(nativeHandle);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// A handle is considered invalid when the underlying pointer is <see cref="IntPtr.Zero"/>.
    /// </remarks>
    public override bool IsInvalid => handle == IntPtr.Zero;

    /// <summary>
    /// Gets whether this wrapper owns the native object's lifetime.
    /// Non-owning wrappers skip native deletion.
    /// </summary>
    public bool OwnsNativeHandle => _ownsNativeHandle;

    /// <summary>
    /// Gets whether this instance manages its own registration in the instance cache.
    /// Defaults to <see langword="false"/>; generated wrappers override with <see langword="true"/>.
    /// </summary>
    protected virtual bool ManagesInstanceCacheLifecycle => false;

    /// <summary>
    /// Gets the per-type instance cache for this wrapper, or <see langword="null"/> if caching is disabled.
    /// </summary>
    protected virtual INativeInstanceCache? InstanceCache => null;

    private bool _ownsNativeHandle;
    private bool _preparedOwnedRegistration;
    private GCHandle _managedKeepAlive;
    private nint _managedKeepAliveKey;

    /// <summary>
    /// Determines whether the runtime type has a managed override of the specified method.
    /// Used by generated code to detect if a user-provided managed override exists for a virtual native method.
    /// </summary>
    /// <param name="runtimeType">The concrete runtime type to inspect.</param>
    /// <param name="declaringType">The declaring type where the base virtual method is defined.</param>
    /// <param name="methodName">The name of the method to check.</param>
    /// <param name="parameterTypes">The parameter types of the method to check.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="runtimeType"/> has an override of the specified method
    /// that is distinct from the implementation in <paramref name="declaringType"/>.
    /// </returns>
    protected static bool HasManagedOverride(Type runtimeType, Type declaringType, string methodName, Type[] parameterTypes)
    {
        var method = runtimeType.GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types: parameterTypes,
            modifiers: null);

        if (method is null)
        {
            return false;
        }

        var declaringMethod = declaringType.GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types: parameterTypes,
            modifiers: null);

        if (declaringMethod is null)
        {
            return false;
        }

        return method.DeclaringType is not null
            && method.DeclaringType != declaringType
            && method.GetBaseDefinition() == declaringMethod.GetBaseDefinition();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Calls <see cref="ReleaseNativeHandle"/> to release the native resource, then unregisters from the instance cache
    /// (unless <see cref="ManagesInstanceCacheLifecycle"/> and <see cref="OwnsNativeHandle"/> are both <see langword="true"/>).
    /// </remarks>
    protected sealed override bool ReleaseHandle()
    {
        var instanceCache = InstanceCache;

        if (handle == IntPtr.Zero)
        {
            SetHandleAsInvalid();
            return true;
        }

        var released = ReleaseNativeHandle(handle);

        if (released)
        {
            if (!(ManagesInstanceCacheLifecycle && OwnsNativeHandle))
            {
                instanceCache?.Unregister(this);
            }

            ClearManagedKeepAlive();
            SetHandleAsInvalid();
        }

        return released;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Unregisters from the instance cache before calling the base <see cref="SafeHandle.Dispose(bool)"/>,
    /// then clears any managed keep-alive reference.
    /// </remarks>
    protected override void Dispose(bool disposing)
    {
        var instanceCache = InstanceCache;

        if (!(ManagesInstanceCacheLifecycle && OwnsNativeHandle) && !IsClosed && !IsInvalid)
        {
            instanceCache?.Unregister(this);
        }

        base.Dispose(disposing);
        ClearManagedKeepAlive();
    }

    /// <summary>
    /// Called by <see cref="ReleaseHandle"/> to perform the actual native resource release.
    /// Overridden by generated code to call the native destructor.
    /// </summary>
    /// <param name="handle">The native handle to release.</param>
    /// <returns>
    /// <see langword="true"/> if the native resource was successfully released;
    /// <see langword="false"/> to retry release later.
    /// </returns>
    protected virtual bool ReleaseNativeHandle(nint handle)
    {
        return true;
    }

    /// <summary>
    /// Prepares an owning wrapper for cache registration. For most wrapper families this is a no-op,
    /// but ref-counted families can acquire their managed-side strong reference here before the cache observes them.
    /// </summary>
    protected void PrepareForOwnedRegistration()
    {
        if (!OwnsNativeHandle || _preparedOwnedRegistration || IsClosed || IsInvalid)
        {
            return;
        }

        lock (_ownershipSync)
        {
            if (!OwnsNativeHandle || _preparedOwnedRegistration || IsClosed || IsInvalid)
            {
                return;
            }

            AcquireOwningHandleCore();
            _preparedOwnedRegistration = true;
        }
    }

    /// <summary>
    /// Promotes an existing borrowed wrapper into the owning wrapper for the same native object.
    /// Returns <see langword="false"/> when the wrapper is already closed/invalid or the runtime-specific
    /// ownership acquisition failed.
    /// </summary>
    public bool TryPromoteToOwningHandle()
    {
        if (OwnsNativeHandle)
        {
            return true;
        }

        if (IsClosed || IsInvalid)
        {
            return false;
        }

        lock (_ownershipSync)
        {
            if (_ownsNativeHandle)
            {
                return true;
            }

            if (IsClosed || IsInvalid)
            {
                return false;
            }

            AcquireOwningHandleCore();
            _ownsNativeHandle = true;
            _preparedOwnedRegistration = true;
            return true;
        }
    }

    /// <summary>
    /// Called when a wrapper first becomes the owning managed reference for its native handle.
    /// Families with extra native ownership bookkeeping can override this hook.
    /// </summary>
    protected virtual void AcquireOwningHandleCore()
    {
    }

    internal void RegisterInInstanceCache(INativeInstanceCache instanceCache)
    {
        if (IsInvalid)
        {
            return;
        }

        PrepareForOwnedRegistration();
        instanceCache.Register(this);
    }

    /// <summary>
    /// Called by the native runtime when the native object is destroyed externally.
    /// Severs the wrapper connection without double-freeing native memory.
    /// </summary>
    protected void DisposeFromNativeRuntimeCore()
    {
        var instanceCache = InstanceCache;

        if (!IsClosed && !IsInvalid)
        {
            instanceCache?.Unregister(this);
        }

        if (!IsClosed)
        {
            SetHandleAsInvalid();
        }

        ClearManagedKeepAlive();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Pins a managed object that must not be collected while this wrapper is alive.
    /// If <paramref name="key"/> matches an existing keep-alive, the provided object is disposed immediately
    /// and the existing keep-alive is preserved (idempotent re-registration).
    /// </summary>
    /// <param name="keepAlive">The disposable managed object to keep alive.</param>
    /// <param name="key">A key identifying the keep-alive slot, typically the native pointer of the associated object.</param>
    /// <exception cref="ArgumentNullException"><paramref name="keepAlive"/> is <see langword="null"/>.</exception>
    protected void SetManagedKeepAliveCore(IDisposable keepAlive, nint key)
    {
        ArgumentNullException.ThrowIfNull(keepAlive);

        if (_managedKeepAlive.IsAllocated && _managedKeepAliveKey == key)
        {
            keepAlive.Dispose();
            return;
        }

        ClearManagedKeepAlive();
        _managedKeepAlive = GCHandle.Alloc(keepAlive);
        _managedKeepAliveKey = key;
    }

    private void ClearManagedKeepAlive()
    {
        if (!_managedKeepAlive.IsAllocated)
        {
            _managedKeepAliveKey = 0;
            return;
        }

        if (_managedKeepAlive.Target is IDisposable keepAlive)
        {
            keepAlive.Dispose();
        }

        _managedKeepAlive.Free();
        _managedKeepAliveKey = 0;
    }

    /// <summary>
    /// Safely casts a value to <see cref="NativeObject"/>, returning <see langword="null"/> for <see langword="null"/> input.
    /// </summary>
    /// <param name="value">The value to cast, expected to be a <see cref="NativeObject"/> wrapper.</param>
    /// <returns>The <see cref="NativeObject"/> instance, or <see langword="null"/> if <paramref name="value"/> is <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="value"/> is not a <see cref="NativeObject"/> and not <see langword="null"/>.</exception>
    public static NativeObject? GetObject(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is not NativeObject native)
        {
            throw new ArgumentException("Expected a SharpCpp-generated native wrapper.", nameof(value));
        }

        return native;
    }

    /// <summary>
    /// Safely casts a value to <see cref="NativeObject"/>, throwing if the input is <see langword="null"/> or not the expected type.
    /// </summary>
    /// <param name="value">The value to cast, expected to be a <see cref="NativeObject"/> wrapper.</param>
    /// <param name="parameterName">The name of the parameter from which <paramref name="value"/> was sourced, used in exception messages.</param>
    /// <returns>The <see cref="NativeObject"/> instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> is not a <see cref="NativeObject"/>.</exception>
    public static NativeObject RequireObject(object value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);

        if (value is not NativeObject native)
        {
            throw new ArgumentException("Expected a SharpCpp-generated native wrapper.", parameterName);
        }

        return native;
    }

    /// <summary>
    /// Extracts the native handle from a wrapper, returning zero for <see langword="null"/> input.
    /// </summary>
    /// <param name="value">The wrapper whose native handle to extract, or <see langword="null"/>.</param>
    /// <returns>The native handle value, or zero if <paramref name="value"/> is <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="value"/> is not a <see cref="NativeObject"/>.</exception>
    /// <exception cref="ObjectDisposedException">The wrapper is closed or invalid.</exception>
    public static nint GetHandle(object? value)
    {
        var native = GetObject(value);

        if (native is null)
        {
            return 0;
        }

        if (native.IsClosed || native.IsInvalid)
        {
            throw new ObjectDisposedException(native.GetType().FullName);
        }

        return native.DangerousGetHandle();
    }

    /// <summary>
    /// Extracts the native handle from a wrapper, throwing if the input is <see langword="null"/> or the wrapper is disposed.
    /// </summary>
    /// <param name="value">The wrapper whose native handle to extract.</param>
    /// <param name="parameterName">The name of the parameter from which <paramref name="value"/> was sourced, used in exception messages.</param>
    /// <returns>The native handle value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> is not a <see cref="NativeObject"/>.</exception>
    /// <exception cref="ObjectDisposedException">The wrapper is closed or invalid.</exception>
    public static nint RequireHandle(object value, string parameterName)
    {
        var native = RequireObject(value, parameterName);

        if (native.IsClosed || native.IsInvalid)
        {
            throw new ObjectDisposedException(native.GetType().FullName);
        }

        return native.DangerousGetHandle();
    }
}
