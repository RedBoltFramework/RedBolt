namespace Urho3D.UI;

public partial class UIElement
{
    public T? CreateChild<T>()
        where T : UIElement, global::SharpCpp.Rbfx.Bindings.Interop.IStaticTypeInfo<T>
    {
        return CreateChild(T.GetTypeStatic()) as T;
    }

    public T? CreateChild<T>(string name)
        where T : UIElement, global::SharpCpp.Rbfx.Bindings.Interop.IStaticTypeInfo<T>
    {
        return CreateChild(T.GetTypeStatic(), name) as T;
    }

    public T? CreateChild<T>(string name, uint index)
        where T : UIElement, global::SharpCpp.Rbfx.Bindings.Interop.IStaticTypeInfo<T>
    {
        return CreateChild(T.GetTypeStatic(), name, index) as T;
    }

    public T? GetChild<T>(string name, bool recursive = false)
        where T : UIElement
    {
        return GetChild(name, recursive) as T;
    }
}
