// C#-only interop marshallers needed for P/Invoke with native Diligent / SDL / rbfx shared libraries.

namespace RedBolt.Math;

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

[CustomMarshaller(typeof(BaseRealVector2<float>), MarshalMode.ManagedToUnmanagedIn, typeof(Vector2fMarshaller))]
[CustomMarshaller(typeof(BaseRealVector2<float>), MarshalMode.ManagedToUnmanagedOut, typeof(Vector2fMarshaller))]
[CustomMarshaller(typeof(BaseRealVector2<float>), MarshalMode.UnmanagedToManagedOut, typeof(Vector2fMarshaller))]
public static class Vector2fMarshaller
{
    public static BaseRealVector2<float> ConvertToUnmanaged(BaseRealVector2<float> managed) => managed;

    public static BaseRealVector2<float> ConvertToManaged(BaseRealVector2<float> unmanaged) => unmanaged;
}

[CustomMarshaller(typeof(BaseIntegerVector2<int>), MarshalMode.ManagedToUnmanagedIn, typeof(Vector2iMarshaller))]
[CustomMarshaller(typeof(BaseIntegerVector2<int>), MarshalMode.ManagedToUnmanagedOut, typeof(Vector2iMarshaller))]
[CustomMarshaller(typeof(BaseIntegerVector2<int>), MarshalMode.UnmanagedToManagedOut, typeof(Vector2iMarshaller))]
public static class Vector2iMarshaller
{
    public static BaseIntegerVector2<int> ConvertToUnmanaged(BaseIntegerVector2<int> managed) => managed;

    public static BaseIntegerVector2<int> ConvertToManaged(BaseIntegerVector2<int> unmanaged) => unmanaged;
}

[CustomMarshaller(typeof(BaseRealVector3<float>), MarshalMode.Default, typeof(Vector3fMarshaller))]
public static class Vector3fMarshaller
{
    public static Vector3fAbi ConvertToUnmanaged(BaseRealVector3<float> managed) =>
        new(managed.X, managed.Y, managed.Z);

    public static BaseRealVector3<float> ConvertToManaged(Vector3fAbi unmanaged) =>
        new(unmanaged.X, unmanaged.Y, unmanaged.Z);
}

[StructLayout(LayoutKind.Sequential)]
public struct Vector3fAbi(float x, float y, float z)
{
    public float X = x;
    public float Y = y;
    public float Z = z;
}

[CustomMarshaller(typeof(Vector4f), MarshalMode.ManagedToUnmanagedIn, typeof(Vector4fMarshaller))]
[CustomMarshaller(typeof(Vector4f), MarshalMode.ManagedToUnmanagedOut, typeof(Vector4fMarshaller))]
[CustomMarshaller(typeof(Vector4f), MarshalMode.UnmanagedToManagedOut, typeof(Vector4fMarshaller))]
public static class Vector4fMarshaller
{
    public static Vector4f ConvertToUnmanaged(Vector4f managed) => managed;

    public static Vector4f ConvertToManaged(Vector4f unmanaged) => unmanaged;
}

[CustomMarshaller(typeof(Color), MarshalMode.ManagedToUnmanagedIn, typeof(ColorMarshaller))]
[CustomMarshaller(typeof(Color), MarshalMode.ManagedToUnmanagedOut, typeof(ColorMarshaller))]
[CustomMarshaller(typeof(Color), MarshalMode.UnmanagedToManagedOut, typeof(ColorMarshaller))]
public static class ColorMarshaller
{
    public static Color ConvertToUnmanaged(Color managed) => managed;

    public static Color ConvertToManaged(Color unmanaged) => unmanaged;
}
