// C#-only interop marshallers needed for P/Invoke with native Diligent / SDL / rbfx shared libraries.
using System.Runtime.InteropServices.Marshalling;

namespace RedBolt;

[CustomMarshaller(typeof(BaseRealVector2<float>), MarshalMode.ManagedToUnmanagedIn, typeof(Vector2fMarshaller))]
[CustomMarshaller(typeof(BaseRealVector2<float>), MarshalMode.ManagedToUnmanagedOut, typeof(Vector2fMarshaller))]
[CustomMarshaller(typeof(BaseRealVector2<float>), MarshalMode.UnmanagedToManagedOut, typeof(Vector2fMarshaller))]
public static class Vector2fMarshaller
{
    public static BaseRealVector2<float> ConvertToUnmanaged(BaseRealVector2<float> managed) => managed;

    public static BaseRealVector2<float> ConvertToManaged(BaseRealVector2<float> unmanaged) => unmanaged;
}

[CustomMarshaller(typeof(Vector4f), MarshalMode.ManagedToUnmanagedIn, typeof(Vector4fMarshaller))]
[CustomMarshaller(typeof(Vector4f), MarshalMode.ManagedToUnmanagedOut, typeof(Vector4fMarshaller))]
[CustomMarshaller(typeof(Vector4f), MarshalMode.UnmanagedToManagedOut, typeof(Vector4fMarshaller))]
public static class Vector4fMarshaller
{
    public static Vector4f ConvertToUnmanaged(Vector4f managed) => managed;

    public static Vector4f ConvertToManaged(Vector4f unmanaged) => unmanaged;
}
