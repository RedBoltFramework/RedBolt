namespace RedBolt;

public sealed class Texture3D : Texture
{
    private readonly Dictionary<uint, byte[]> levelData = new();

    public Texture3D(string name)
        : base(name)
    {
    }

    public bool SetSize(int width, int height, int depth, TextureFormat format)
    {
        if (width <= 0 || height <= 0 || depth <= 0)
            return false;

        SetDimensions(width, height, depth, format);
        levelData.Clear();
        RefreshTotalMemoryUse();
        return true;
    }

    public bool SetData(uint level, int x, int y, int z, int width, int height, int depth, ReadOnlySpan<byte> data)
    {
        if (level >= Levels || x < 0 || y < 0 || z < 0 || width <= 0 || height <= 0 || depth <= 0)
            return false;

        var levelWidth = GetLevelWidth(level);
        var levelHeight = GetLevelHeight(level);
        var levelDepth = GetLevelDepth(level);
        if (x + width > levelWidth || y + height > levelHeight || z + depth > levelDepth)
            return false;

        var bytesPerPixel = (int)GetComponents();
        if (bytesPerPixel == 0)
            return false;

        var expected = checked(width * height * depth * bytesPerPixel);
        if (data.Length != expected)
            return false;

        if (!levelData.TryGetValue(level, out var levelBytes))
        {
            levelBytes = new byte[checked(levelWidth * levelHeight * levelDepth * bytesPerPixel)];
            levelData[level] = levelBytes;
        }

        for (var slice = 0; slice < depth; ++slice)
        {
            for (var row = 0; row < height; ++row)
            {
                var sourceOffset = ((slice * height) + row) * width * bytesPerPixel;
                var destinationOffset = ((((z + slice) * levelHeight) + (y + row)) * levelWidth + x) * bytesPerPixel;
                data.Slice(sourceOffset, width * bytesPerPixel).CopyTo(levelBytes.AsSpan(destinationOffset, width * bytesPerPixel));
            }
        }

        RefreshTotalMemoryUse();
        return true;
    }

    public bool SetData(uint level, int x, int y, int z, int width, int height, int depth, byte[] data) => SetData(level, x, y, z, width, height, depth, data.AsSpan());

    public byte[]? GetData(uint level)
    {
        return levelData.TryGetValue(level, out var bytes) ? bytes.ToArray() : null;
    }

    private void RefreshTotalMemoryUse()
    {
        RefreshMemoryUse((uint)levelData.Sum(static item => item.Value.Length));
    }
}
