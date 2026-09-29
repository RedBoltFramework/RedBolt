// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

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

[CustomMarshaller(typeof(BaseIntegerVector2<int>), MarshalMode.Default, typeof(Vector2iIndirectMarshaller))]
public static class Vector2iIndirectMarshaller
{
    public static nint ConvertToUnmanaged(BaseIntegerVector2<int> managed) =>
        SharpCpp.Support.IndirectValueMarshaller<BaseIntegerVector2<int>>.ConvertToUnmanaged(managed);

    public static BaseIntegerVector2<int> ConvertToManaged(nint unmanaged) =>
        SharpCpp.Support.IndirectValueMarshaller<BaseIntegerVector2<int>>.ConvertToManaged(unmanaged);

    public static void Free(nint unmanaged) =>
        SharpCpp.Support.IndirectValueMarshaller<BaseIntegerVector2<int>>.Free(unmanaged);
}

[CustomMarshaller(typeof(BaseRealVector2<float>), MarshalMode.Default, typeof(Vector2fIndirectMarshaller))]
public static class Vector2fIndirectMarshaller
{
    public static nint ConvertToUnmanaged(BaseRealVector2<float> managed) =>
        SharpCpp.Support.IndirectValueMarshaller<BaseRealVector2<float>>.ConvertToUnmanaged(managed);

    public static BaseRealVector2<float> ConvertToManaged(nint unmanaged) =>
        SharpCpp.Support.IndirectValueMarshaller<BaseRealVector2<float>>.ConvertToManaged(unmanaged);

    public static void Free(nint unmanaged) =>
        SharpCpp.Support.IndirectValueMarshaller<BaseRealVector2<float>>.Free(unmanaged);
}

[CustomMarshaller(typeof(BaseRealVector3<float>), MarshalMode.Default, typeof(Vector3fIndirectMarshaller))]
public static class Vector3fIndirectMarshaller
{
    public static nint ConvertToUnmanaged(BaseRealVector3<float> managed) =>
        SharpCpp.Support.IndirectValueMarshaller<BaseRealVector3<float>>.ConvertToUnmanaged(managed);

    public static BaseRealVector3<float> ConvertToManaged(nint unmanaged) =>
        SharpCpp.Support.IndirectValueMarshaller<BaseRealVector3<float>>.ConvertToManaged(unmanaged);

    public static void Free(nint unmanaged) =>
        SharpCpp.Support.IndirectValueMarshaller<BaseRealVector3<float>>.Free(unmanaged);
}

[CustomMarshaller(typeof(Vector4f), MarshalMode.Default, typeof(Vector4fIndirectMarshaller))]
public static class Vector4fIndirectMarshaller
{
    public static nint ConvertToUnmanaged(Vector4f managed) =>
        SharpCpp.Support.IndirectValueMarshaller<Vector4f>.ConvertToUnmanaged(managed);

    public static Vector4f ConvertToManaged(nint unmanaged) =>
        SharpCpp.Support.IndirectValueMarshaller<Vector4f>.ConvertToManaged(unmanaged);

    public static void Free(nint unmanaged) => SharpCpp.Support.IndirectValueMarshaller<Vector4f>.Free(unmanaged);
}

[CustomMarshaller(typeof(Color), MarshalMode.Default, typeof(ColorIndirectMarshaller))]
public static class ColorIndirectMarshaller
{
    public static nint ConvertToUnmanaged(Color managed) =>
        SharpCpp.Support.IndirectValueMarshaller<Color>.ConvertToUnmanaged(managed);

    public static Color ConvertToManaged(nint unmanaged) =>
        SharpCpp.Support.IndirectValueMarshaller<Color>.ConvertToManaged(unmanaged);

    public static void Free(nint unmanaged) => SharpCpp.Support.IndirectValueMarshaller<Color>.Free(unmanaged);
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

[CustomMarshaller(typeof(BaseRealVector3<double>), MarshalMode.Default, typeof(Vector3dMarshaller))]
public static class Vector3dMarshaller
{
    public static Vector3dAbi ConvertToUnmanaged(BaseRealVector3<double> managed) =>
        new(managed.X, managed.Y, managed.Z);

    public static BaseRealVector3<double> ConvertToManaged(Vector3dAbi unmanaged) =>
        new(unmanaged.X, unmanaged.Y, unmanaged.Z);
}

[StructLayout(LayoutKind.Sequential)]
public struct Vector3dAbi(double x, double y, double z)
{
    public double X = x;
    public double Y = y;
    public double Z = z;
}

// Vector4f is a blittable sequential struct, so it is its own ABI type. Default is included so
// the marshaller can also be used for in/ref parameters (Jolt Vec4&/const Vec4&).
[CustomMarshaller(typeof(Vector4f), MarshalMode.Default, typeof(Vector4fMarshaller))]
[CustomMarshaller(typeof(Vector4f), MarshalMode.ManagedToUnmanagedIn, typeof(Vector4fMarshaller))]
[CustomMarshaller(typeof(Vector4f), MarshalMode.ManagedToUnmanagedOut, typeof(Vector4fMarshaller))]
[CustomMarshaller(typeof(Vector4f), MarshalMode.UnmanagedToManagedOut, typeof(Vector4fMarshaller))]
public static class Vector4fMarshaller
{
    public static Vector4f ConvertToUnmanaged(Vector4f managed) => managed;

    public static Vector4f ConvertToManaged(Vector4f unmanaged) => unmanaged;
}

[CustomMarshaller(typeof(Rect), MarshalMode.Default, typeof(RectMarshaller))]
public static class RectMarshaller
{
    public static Rect ConvertToUnmanaged(Rect managed) => managed;

    public static Rect ConvertToManaged(Rect unmanaged) => unmanaged;
}

[CustomMarshaller(typeof(Color), MarshalMode.ManagedToUnmanagedIn, typeof(ColorMarshaller))]
[CustomMarshaller(typeof(Color), MarshalMode.ManagedToUnmanagedOut, typeof(ColorMarshaller))]
[CustomMarshaller(typeof(Color), MarshalMode.UnmanagedToManagedOut, typeof(ColorMarshaller))]
public static class ColorMarshaller
{
    public static Color ConvertToUnmanaged(Color managed) => managed;

    public static Color ConvertToManaged(Color unmanaged) => unmanaged;
}
