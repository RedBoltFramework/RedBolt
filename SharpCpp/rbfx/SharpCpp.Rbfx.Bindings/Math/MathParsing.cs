using System;
using System.Globalization;

namespace Urho3D.Math;

internal static class MathParsing
{
    public static DoubleVector2 ToDoubleVector2(string value)
    {
        var components = ParseDoubles(value, 2);
        return new DoubleVector2(components[0], components[1]);
    }

    public static DoubleVector3 ToDoubleVector3(string value)
    {
        var components = ParseDoubles(value, 3);
        return new DoubleVector3(components[0], components[1], components[2]);
    }

    public static Vector2 ToVector2(string value)
    {
        var components = ParseFloats(value, 2);
        return new Vector2(components[0], components[1]);
    }

    public static Vector3 ToVector3(string value)
    {
        var components = ParseFloats(value, 3);
        return new Vector3(components[0], components[1], components[2]);
    }

    public static Vector4 ToVector4(string value)
    {
        var components = ParseFloats(value, 4);
        return new Vector4(components[0], components[1], components[2], components[3]);
    }

    public static Quaternion ToQuaternion(string value)
    {
        var components = ParseFloats(value, 4);
        return new Quaternion(components[0], components[1], components[2], components[3]);
    }

    public static IntVector2 ToIntVector2(string value)
    {
        var components = ParseInts(value, 2);
        return new IntVector2(components[0], components[1]);
    }

    public static IntVector3 ToIntVector3(string value)
    {
        var components = ParseInts(value, 3);
        return new IntVector3(components[0], components[1], components[2]);
    }

    public static IntRect ToIntRect(string value)
    {
        var components = ParseInts(value, 4);
        return new IntRect(components[0], components[1], components[2], components[3]);
    }

    public static Rect ToRect(string value)
    {
        var components = ParseFloats(value, 4);
        return new Rect(components[0], components[1], components[2], components[3]);
    }

    public static Matrix2 ToMatrix2(string value)
    {
        var components = ParseFloats(value, 4);
        return new Matrix2(components[0], components[1], components[2], components[3]);
    }

    public static Matrix3 ToMatrix3(string value)
    {
        var components = ParseFloats(value, 9);
        return new Matrix3(
            components[0], components[1], components[2],
            components[3], components[4], components[5],
            components[6], components[7], components[8]);
    }

    public static Matrix3x4 ToMatrix3x4(string value)
    {
        var components = ParseFloats(value, 12);
        return new Matrix3x4(
            components[0], components[1], components[2], components[3],
            components[4], components[5], components[6], components[7],
            components[8], components[9], components[10], components[11]);
    }

    public static Matrix4 ToMatrix4(string value)
    {
        var components = ParseFloats(value, 16);
        return new Matrix4(
            components[0], components[1], components[2], components[3],
            components[4], components[5], components[6], components[7],
            components[8], components[9], components[10], components[11],
            components[12], components[13], components[14], components[15]);
    }

    private static float[] ParseFloats(string value, int expectedCount)
    {
        var parts = Tokenize(value, expectedCount);
        var result = new float[parts.Length];

        for (var index = 0; index < parts.Length; index++)
        {
            result[index] = float.Parse(parts[index], CultureInfo.InvariantCulture);
        }

        return result;
    }

    private static double[] ParseDoubles(string value, int expectedCount)
    {
        var parts = Tokenize(value, expectedCount);
        var result = new double[parts.Length];

        for (var index = 0; index < parts.Length; index++)
        {
            result[index] = double.Parse(parts[index], CultureInfo.InvariantCulture);
        }

        return result;
    }

    private static int[] ParseInts(string value, int expectedCount)
    {
        var parts = Tokenize(value, expectedCount);
        var result = new int[parts.Length];

        for (var index = 0; index < parts.Length; index++)
        {
            result[index] = int.Parse(parts[index], CultureInfo.InvariantCulture);
        }

        return result;
    }

    private static string[] Tokenize(string value, int expectedCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var parts = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length != expectedCount)
        {
            throw new FormatException($"Expected {expectedCount} component(s) but found {parts.Length}.");
        }

        return parts;
    }
}