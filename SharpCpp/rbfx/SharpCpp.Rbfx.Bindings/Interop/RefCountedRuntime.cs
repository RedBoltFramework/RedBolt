using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Urho3D.Interop;

internal static partial class RefCountedRuntime
{
    [LibraryImport("SharpCpp.Rbfx.Native", EntryPoint = "sharpcpp_rbfx_refcounted_get_script_object")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static partial nint GetScriptObjectHandle(nint self);

    [LibraryImport("SharpCpp.Rbfx.Native", EntryPoint = "sharpcpp_rbfx_refcounted_set_script_object")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static partial void SetScriptObjectHandle(nint self, nint gcHandle, [MarshalAs(UnmanagedType.I1)] bool isStrong);

    [LibraryImport("SharpCpp.Rbfx.Native", EntryPoint = "sharpcpp_rbfx_refcounted_reset_script_object")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static partial void ResetScriptObjectHandle(nint self);

    [LibraryImport("SharpCpp.Rbfx.Native", EntryPoint = "sharpcpp_rbfx_refcounted_add_ref")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static partial void AddRef(nint self);

    [LibraryImport("SharpCpp.Rbfx.Native", EntryPoint = "sharpcpp_rbfx_refcounted_release_ref")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static partial void ReleaseRef(nint self);
}
