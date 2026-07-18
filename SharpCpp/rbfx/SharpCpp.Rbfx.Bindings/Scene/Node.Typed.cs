namespace Urho3D.Scene;

public partial class Node
{
    public Node? CreateChild(string? name = null)
    {
        using var nativeName = new global::Ea.String(name ?? string.Empty);
        return CreateChild(nativeName);
    }

    public T? CreateComponent<T>(uint id = 0)
        where T : Component, global::SharpCpp.Rbfx.Bindings.Interop.IStaticTypeInfo<T>
    {
        return CreateComponent(T.GetTypeStatic(), id) as T;
    }

    public T? GetOrCreateComponent<T>(uint id = 0)
        where T : Component, global::SharpCpp.Rbfx.Bindings.Interop.IStaticTypeInfo<T>
    {
        return GetOrCreateComponent(T.GetTypeStatic(), id) as T;
    }

    public void RemoveComponent<T>()
        where T : Component, global::SharpCpp.Rbfx.Bindings.Interop.IStaticTypeInfo<T>
    {
        RemoveComponent(T.GetTypeStatic());
    }

    public T? GetComponent<T>()
        where T : Component, global::SharpCpp.Rbfx.Bindings.Interop.IStaticTypeInfo<T>
    {
        return GetComponent(T.GetTypeStatic()) as T;
    }

    public T? GetParentComponent<T>()
        where T : Component, global::SharpCpp.Rbfx.Bindings.Interop.IStaticTypeInfo<T>
    {
        return GetParentComponent(T.GetTypeStatic()) as T;
    }

    public T? GetDerivedComponent<T>()
        where T : Component, global::SharpCpp.Rbfx.Bindings.Interop.IStaticTypeInfo<T>
    {
        return GetDerivedComponent(T.GetTypeStatic()) as T;
    }
}
