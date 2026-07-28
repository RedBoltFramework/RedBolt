namespace RedBolt.Tests;

using RedBolt.Core;

public class VariantTypeUtilityTests
{
    [Fact]
    public void VariantTypeSizesMatchRbfx()
    {
        var pointer64 = IntPtr.Size == 8;
        (VariantType Type, int Size)[] expected =
        [
            (VariantType.None, 0),
            (VariantType.Int, 4),
            (VariantType.Int64, 8),
            (VariantType.Bool, 1),
            (VariantType.Float, 4),
            (VariantType.Double, 8),
            (VariantType.Vector2, 8),
            (VariantType.Vector3, 12),
            (VariantType.DoubleVector2, 16),
            (VariantType.DoubleVector3, 24),
            (VariantType.Vector4, 16),
            (VariantType.Quaternion, 16),
            (VariantType.Color, 16),
            (VariantType.String, pointer64 ? 24 : 16),
            (VariantType.Buffer, pointer64 ? 32 : 16),
            (VariantType.ResourceRef, pointer64 ? 32 : 20),
            (VariantType.ResourceRefList, pointer64 ? 40 : 20),
            (VariantType.VariantVector, pointer64 ? 32 : 16),
            (VariantType.StringVector, pointer64 ? 32 : 16),
            (VariantType.VariantMap, pointer64 ? 48 : 32),
            (VariantType.Rect, 16),
            (VariantType.IntRect, 16),
            (VariantType.Vector2i, 8),
            (VariantType.Vector3i, 12),
            (VariantType.Matrix3, 36),
            (VariantType.Matrix3x4, 48),
            (VariantType.Matrix4, 64),
            (VariantType.StringVariantMap, pointer64 ? 48 : 32)
        ];

        foreach (var item in expected)
            Assert.Equal(item.Size, VariantTypeUtility.GetVariantTypeSize(item.Type));
    }
}
