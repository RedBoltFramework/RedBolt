using Urho3D.Script;

namespace Urho3D.Core;

public partial class Context
{
    public static Context? Instance => GetInstance();

    internal static void ClearRuntimeApiIfCurrent(ScriptRuntimeApi impl)
    {
        ArgumentNullException.ThrowIfNull(impl);

        var current = global::Urho3D.Script.Script.GetRuntimeApi();
        if (current is null)
        {
            return;
        }

        var currentHandle = current.IsClosed || current.IsInvalid
            ? IntPtr.Zero
            : current.DangerousGetHandle();
        var expectedHandle = impl.IsClosed || impl.IsInvalid
            ? IntPtr.Zero
            : impl.DangerousGetHandle();

        if (currentHandle == IntPtr.Zero || currentHandle != expectedHandle)
        {
            return;
        }

        global::Urho3D.Script.Script.__PInvoke.SetRuntimeApi(null);
    }

    public static void SetRuntimeApi(ScriptRuntimeApi? impl)
    {
        var previous = global::Urho3D.Script.Script.GetRuntimeApi();

        if (impl is null)
        {
            global::Urho3D.Script.Script.__PInvoke.SetRuntimeApi(null);
        }
        else
        {
            global::Urho3D.Script.Script.SetRuntimeApi(impl);
        }

        if (previous is not null && !ReferenceEquals(previous, impl))
        {
            previous.Dispose();
        }
    }
}
