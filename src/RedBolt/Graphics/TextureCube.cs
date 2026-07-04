namespace RedBolt;

public enum CubeMapFace
{
    PositiveX,
    NegativeX,
    PositiveY,
    NegativeY,
    PositiveZ,
    NegativeZ
}

public sealed class TextureCube : Texture
{
    private readonly Dictionary<(CubeMapFace Face, uint Level), byte[]> faceData = new();

    public TextureCube(string name)
        : base(name)
    {
    }

    public bool SetSize(int size, TextureFormat format)
    {
        if (size <= 0)
            return false;

        SetDimensions(size, size, 1, format);
        faceData.Clear();
        RefreshTotalMemoryUse();
        return true;
    }

    public bool SetData(CubeMapFace face, uint level, int x, int y, int width, int height, ReadOnlySpan<byte> data)
    {
        if (level >= Levels || x < 0 || y < 0 || width <= 0 || height <= 0)
            return false;

        var levelSize = GetLevelWidth(level);
        if (x + width > levelSize || y + height > levelSize)
            return false;

        var bytesPerPixel = (int)GetComponents();
        if (bytesPerPixel == 0)
            return false;

        var expected = checked(width * height * bytesPerPixel);
        if (data.Length != expected)
            return false;

        var key = (face, level);
        if (!faceData.TryGetValue(key, out var levelBytes))
        {
            levelBytes = new byte[checked(levelSize * levelSize * bytesPerPixel)];
            faceData[key] = levelBytes;
        }

        for (var row = 0; row < height; ++row)
        {
            var sourceOffset = row * width * bytesPerPixel;
            var destinationOffset = ((y + row) * levelSize + x) * bytesPerPixel;
            data.Slice(sourceOffset, width * bytesPerPixel).CopyTo(levelBytes.AsSpan(destinationOffset, width * bytesPerPixel));
        }

        RefreshTotalMemoryUse();
        return true;
    }

    public bool SetData(CubeMapFace face, uint level, int x, int y, int width, int height, byte[] data) => SetData(face, level, x, y, width, height, data.AsSpan());

    public byte[]? GetData(CubeMapFace face, uint level)
    {
        return faceData.TryGetValue((face, level), out var bytes) ? bytes.ToArray() : null;
    }

    private void RefreshTotalMemoryUse()
    {
        RefreshMemoryUse((uint)faceData.Sum(static item => item.Value.Length));
    }
}
