namespace Urho3D.Core;

public partial class Context
{
    public T? GetSubsystem<T>()
        where T : Object, global::SharpCpp.Rbfx.Bindings.Interop.IStaticTypeInfo<T>
    {
        return GetSubsystem(T.GetTypeStatic()) as T;
    }
}
