using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SharpCpp.Rbfx.Bindings.Interop;

public static partial class ManagedTypeInfoRegistry
{
    public static global::Urho3D.Core.TypeInfo GetOrCreate(string typeName, global::Urho3D.Core.TypeInfo? baseTypeInfo)
    {
        ArgumentException.ThrowIfNullOrEmpty(typeName);

        var handle = __PInvoke.GetOrCreateManagedTypeInfo(typeName, baseTypeInfo);
        if (handle == 0)
        {
            throw new InvalidOperationException($"Failed to create managed TypeInfo for '{typeName}'.");
        }

        return global::Urho3D.Core.TypeInfo.CreateManagedWrapper(handle, ownsHandle: false)
            ?? throw new InvalidOperationException($"Failed to wrap managed TypeInfo for '{typeName}'.");
    }

    private static partial class __PInvoke
    {
        [LibraryImport("SharpCpp.Rbfx.Native", EntryPoint = "sharpcpp_rbfx_get_or_create_managed_type_info", StringMarshalling = StringMarshalling.Utf8)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static partial nint GetOrCreateManagedTypeInfo(string typeName, global::Urho3D.Core.TypeInfo? baseTypeInfo);
    }
}
