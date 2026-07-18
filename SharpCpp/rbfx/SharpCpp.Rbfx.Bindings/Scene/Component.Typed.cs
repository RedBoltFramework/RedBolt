namespace Urho3D.Scene;

public partial class Component
{
    public T? GetComponent<T>()
        where T : Component, global::SharpCpp.Rbfx.Bindings.Interop.IStaticTypeInfo<T>
    {
        return GetComponent(T.GetTypeStatic()) as T;
    }
}
