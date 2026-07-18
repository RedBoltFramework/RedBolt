namespace Urho3D.Interop;

public static class RefCountedRuntimeHelpers
{
    public static void DisposeFromNativeRuntime(RefCountedNativeObject? instance)
    {
        instance?.DisposeFromNativeRuntime();
    }
}
