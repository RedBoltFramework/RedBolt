using System.Reflection;

namespace RedBolt.Tests;

public sealed class GraphicsMatrixPackingTests
{
    [Fact]
    public void WriteMatrix4_uploads_raw_rbfx_row_major_matrix_memory()
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
        Assert.Equal(matrix.Data, uploaded);
    }

    [Fact]
    public void Fullscreen_camera_buffer_flips_projection_when_requested()
    {
        var method = typeof(Graphics).GetMethod("BuildFullscreenCameraBufferData", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Graphics.BuildFullscreenCameraBufferData was not found.");

        var regular = Assert.IsType<byte[]>(method.Invoke(null, [new Vector4(0.5f), new Vector2(0.25f, 0.5f), false]));
        var flipped = Assert.IsType<byte[]>(method.Invoke(null, [new Vector4(0.5f), new Vector2(0.25f, 0.5f), true]));

        Assert.Equal(1.0f, BitConverter.ToSingle(regular, 128 + 5 * sizeof(float)));
        Assert.Equal(-1.0f, BitConverter.ToSingle(flipped, 128 + 5 * sizeof(float)));
        Assert.Equal(0.25f, BitConverter.ToSingle(flipped, 304));
        Assert.Equal(0.5f, BitConverter.ToSingle(flipped, 308));
    }
}
