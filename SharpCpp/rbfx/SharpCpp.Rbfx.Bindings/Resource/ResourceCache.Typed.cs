namespace Urho3D.Resource;

public partial class ResourceCache
{
    public T? GetResource<T>(string name, bool sendEventOnFailure = true)
        where T : global::Urho3D.Core.Object, global::SharpCpp.Rbfx.Bindings.Interop.IStaticTypeInfo<T>
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        return GetResource(T.GetTypeStatic(), name, sendEventOnFailure) as T;
    }

    public T? GetTempResource<T>(string name, bool sendEventOnFailure = true)
        where T : global::Urho3D.Core.Object, global::SharpCpp.Rbfx.Bindings.Interop.IStaticTypeInfo<T>
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        return GetTempResource(T.GetTypeStatic(), name, sendEventOnFailure) as T;
    }

    public T? GetExistingResource<T>(string name)
        where T : global::Urho3D.Core.Object, global::SharpCpp.Rbfx.Bindings.Interop.IStaticTypeInfo<T>
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        return GetExistingResource(T.GetTypeStatic(), name) as T;
    }
}
