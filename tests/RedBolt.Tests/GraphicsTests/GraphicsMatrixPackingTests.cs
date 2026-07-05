using System.Reflection;

namespace RedBolt.Tests;

public sealed class GraphicsMatrixPackingTests
{
    [Fact]
    public void WriteMatrix4_uploads_transposed_layout_for_rbfx_glsl_row_vectors()
    {
        var matrix = new Matrix4(
            1.0f, 2.0f, 3.0f, 4.0f,
            5.0f, 6.0f, 7.0f, 8.0f,
            9.0f, 10.0f, 11.0f, 12.0f,
            13.0f, 14.0f, 15.0f, 16.0f);
        var buffer = new byte[16 * sizeof(float)];

        var method = typeof(Graphics).GetMethod("WriteMatrix4", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Graphics.WriteMatrix4 was not found.");
        method.Invoke(null, [buffer, 0, matrix]);

        var uploaded = new float[16];
        Buffer.BlockCopy(buffer, 0, uploaded, 0, buffer.Length);
        Assert.Equal(matrix.Transposed.Data, uploaded);
    }
}
