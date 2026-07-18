using SharpCpp.Support;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Urho3D.Interop;

public sealed class RefCountedInstanceCache : INativeInstanceCache
{
    public static RefCountedInstanceCache Shared { get; } = new();

    private RefCountedInstanceCache()
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetExisting(nint handle, bool ownsHandle, out NativeObject? instance)
    {
        if (handle == IntPtr.Zero)
        {
            instance = null;
            return false;
        }

        var storedHandle = RefCountedRuntime.GetScriptObjectHandle(handle);
        if (storedHandle == IntPtr.Zero)
        {
            instance = null;
            return false;
        }

        var gcHandle = GCHandle.FromIntPtr(storedHandle);
        instance = gcHandle.Target as NativeObject;

        if (instance is not null &&
            !instance.IsClosed &&
            (!ownsHandle || instance.OwnsNativeHandle || instance.TryPromoteToOwningHandle()))
        {
            return true;
        }

        return ResetStaleHandle(handle, storedHandle, out instance);
    }

    public void Register(NativeObject instance)
    {
        if (instance.IsClosed || instance.IsInvalid)
        {
            return;
        }

        var handle = instance.DangerousGetHandle();
        var currentHandle = RefCountedRuntime.GetScriptObjectHandle(handle);

        if (currentHandle != IntPtr.Zero)
        {
            var gcHandle = GCHandle.FromIntPtr(currentHandle);
            if (gcHandle.Target is NativeObject existing)
            {
                if (existing.IsClosed || existing.IsInvalid)
                {
                    RefCountedRuntime.ResetScriptObjectHandle(handle);
                    FreeHandle(currentHandle);
                    currentHandle = IntPtr.Zero;
                }
                else if (existing.OwnsNativeHandle && !instance.OwnsNativeHandle && !ReferenceEquals(existing, instance))
                {
                    return;
                }
            }
            else
            {
                RefCountedRuntime.ResetScriptObjectHandle(handle);
                FreeHandle(currentHandle);
                currentHandle = IntPtr.Zero;
            }
        }

        var handleType = instance.OwnsNativeHandle ? GCHandleType.Normal : GCHandleType.Weak;
        var replacementHandle = GCHandle.ToIntPtr(GCHandle.Alloc(instance, handleType));
        RefCountedRuntime.SetScriptObjectHandle(handle, replacementHandle, instance.OwnsNativeHandle);

        if (currentHandle != IntPtr.Zero && currentHandle != replacementHandle)
        {
            FreeHandle(currentHandle);
        }
    }

    public void Unregister(NativeObject instance)
    {
        if (instance.IsClosed || instance.IsInvalid)
        {
            return;
        }

        var handle = instance.DangerousGetHandle();
        var currentHandle = RefCountedRuntime.GetScriptObjectHandle(handle);
        if (currentHandle == IntPtr.Zero)
        {
            return;
        }

        var gcHandle = GCHandle.FromIntPtr(currentHandle);
        var current = gcHandle.Target as NativeObject;

        if (current is not null && !ReferenceEquals(current, instance))
        {
            return;
        }

        RefCountedRuntime.ResetScriptObjectHandle(handle);
        FreeHandle(currentHandle);
    }

    private static void FreeHandle(nint handle)
    {
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var gcHandle = GCHandle.FromIntPtr(handle);
        if (gcHandle.IsAllocated)
        {
            gcHandle.Free();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool ResetStaleHandle(nint handle, nint storedHandle, out NativeObject? instance)
    {
        RefCountedRuntime.ResetScriptObjectHandle(handle);
        FreeHandle(storedHandle);
        instance = null;
        return false;
    }
}
