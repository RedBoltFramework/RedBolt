namespace RedBolt;

public sealed class Texture2DArray : Texture
{
    private readonly Dictionary<(uint Slice, uint Level), byte[]> sliceData = new();

    public Texture2DArray(string name)
        : base(name)
    {
    }

    public uint Layers => (uint)Depth;

    public bool SetLayers(uint layers, int width, int height, TextureFormat format)
    {
        if (layers == 0 || width <= 0 || height <= 0)
            return false;

        SetDimensions(width, height, (int)layers, format);
        sliceData.Clear();
        RefreshTotalMemoryUse();
        return true;
    }

    public bool SetData(uint slice, uint level, int x, int y, int width, int height, ReadOnlySpan<byte> data)
    {
        if (slice >= Layers || level >= Levels || x < 0 || y < 0 || width <= 0 || height <= 0)
            return false;

        var levelWidth = GetLevelWidth(level);
        var levelHeight = GetLevelHeight(level);
        if (x + width > levelWidth || y + height > levelHeight)
            return false;

        var bytesPerPixel = (int)GetComponents();
        if (bytesPerPixel == 0)
            return false;

        var expected = checked(width * height * bytesPerPixel);
        if (data.Length != expected)
            return false;

        var key = (slice, level);
        if (!sliceData.TryGetValue(key, out var levelBytes))
        {
            levelBytes = new byte[checked(levelWidth * levelHeight * bytesPerPixel)];
            sliceData[key] = levelBytes;
        }

        for (var row = 0; row < height; ++row)
        {
            var sourceOffset = row * width * bytesPerPixel;
            var destinationOffset = ((y + row) * levelWidth + x) * bytesPerPixel;
            data.Slice(sourceOffset, width * bytesPerPixel).CopyTo(levelBytes.AsSpan(destinationOffset, width * bytesPerPixel));
        }

        RefreshTotalMemoryUse();
        return true;
    }

    public bool SetData(uint slice, uint level, int x, int y, int width, int height, byte[] data) => SetData(slice, level, x, y, width, height, data.AsSpan());

    public byte[]? GetData(uint slice, uint level)
    {
        return sliceData.TryGetValue((slice, level), out var bytes) ? bytes.ToArray() : null;
    }

    private void RefreshTotalMemoryUse()
    {
        RefreshMemoryUse((uint)sliceData.Sum(static item => item.Value.Length));
    }
}
