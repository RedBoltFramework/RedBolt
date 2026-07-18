namespace SharpCpp.Rbfx.Bindings.Interop;

public interface IStaticTypeInfo<TSelf>
    where TSelf : global::Urho3D.Core.Object, IStaticTypeInfo<TSelf>
{
    public static abstract global::Urho3D.Math.StringHash GetTypeStatic();
}
