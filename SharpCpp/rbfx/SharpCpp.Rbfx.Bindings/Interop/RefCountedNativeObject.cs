using SharpCpp.Support;

namespace Urho3D.Interop;

public abstract class RefCountedNativeObject : NativeObject
{
    protected RefCountedNativeObject(nint nativeHandle, bool ownsHandle)
        : base(nativeHandle, ownsHandle)
    {
    }

    protected override INativeInstanceCache? InstanceCache => RefCountedInstanceCache.Shared;

    protected override bool ManagesInstanceCacheLifecycle => true;

    protected sealed override bool ReleaseNativeHandle(nint handle)
    {
        InstanceCache?.Unregister(this);

        if (OwnsNativeHandle)
        {
            RefCountedRuntime.ReleaseRef(handle);
        }

        return true;
    }

    protected sealed override void AcquireOwningHandleCore()
    {
        RefCountedRuntime.AddRef(DangerousGetHandle());
    }

    internal void DisposeFromNativeRuntime()
    {
        DisposeFromNativeRuntimeCore();
    }
}
