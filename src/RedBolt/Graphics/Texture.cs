namespace RedBolt;

public enum TextureAddressMode
{
    Wrap,
    Mirror,
    Clamp,
    Border
}

public enum TextureCoordinate
{
    U,
    V,
    W
}

public enum TextureFormat
{
    Unknown,
    Depth24Stencil8,
    R8,
    RG16F,
    RGB8,
    RGBA8,
    RGBA16F
}

public abstract class Texture : ResourceWithMetadata
{
    private readonly Dictionary<TextureCoordinate, TextureAddressMode> addressModes = new()
    {
        [TextureCoordinate.U] = TextureAddressMode.Wrap,
        [TextureCoordinate.V] = TextureAddressMode.Wrap,
        [TextureCoordinate.W] = TextureAddressMode.Wrap
    };

    private readonly int[] mipsToSkip = [2, 1, 0];

    protected Texture(string name)
        : base(name)
    {
    }

    public TextureFormat Format { get; protected set; } = TextureFormat.Unknown;

    public uint Levels { get; protected set; } = 1u;

    public int Width { get; protected set; }

    public int Height { get; protected set; }

    public int Depth { get; protected set; } = 1;

    public TextureFilterMode FilterMode { get; private set; } = TextureFilterMode.Default;

    public uint Anisotropy { get; private set; }

    public bool ShadowCompare { get; private set; }

    public bool Linear { get; private set; }

    public bool SRGB { get; private set; }

    public Texture? BackupTexture { get; private set; }

    public bool IsCompressed() => false;

    public void SetFilterMode(TextureFilterMode mode)
    {
        FilterMode = mode;
    }

    public TextureFilterMode GetFilterMode() => FilterMode;

    public void SetAddressMode(TextureCoordinate coordinate, TextureAddressMode mode)
    {
        addressModes[coordinate] = mode;
    }

    public TextureAddressMode GetAddressMode(TextureCoordinate coordinate) => addressModes.GetValueOrDefault(coordinate, TextureAddressMode.Wrap);

    public void SetAnisotropy(uint level)
    {
        Anisotropy = level;
    }

    public uint GetAnisotropy() => Anisotropy;

    public void SetShadowCompare(bool enable)
    {
        ShadowCompare = enable;
    }

    public bool GetShadowCompare() => ShadowCompare;

    public void SetNumLevels(uint levels)
    {
        Levels = levels;
    }

    public uint GetLevels() => Levels;

    public void SetLinear(bool linear)
    {
        Linear = linear;
    }

    public bool GetLinear() => Linear;

    public void SetSRGB(bool enable)
    {
        SRGB = enable;
    }

    public bool GetSRGB() => SRGB;

    public void SetBackupTexture(Texture? texture)
    {
        BackupTexture = texture;
    }

    public Texture? GetBackupTexture() => BackupTexture;

    public void SetMipsToSkip(MaterialQuality quality, int toSkip)
    {
        var index = Math.Clamp((int)quality, 0, mipsToSkip.Length - 1);
        mipsToSkip[index] = Math.Max(toSkip, 0);
        for (var i = 1; i < mipsToSkip.Length; ++i)
            mipsToSkip[i] = Math.Min(mipsToSkip[i], mipsToSkip[i - 1]);
    }

    public int GetMipsToSkip(MaterialQuality quality)
    {
        var index = Math.Clamp((int)quality, 0, mipsToSkip.Length - 1);
        return mipsToSkip[index];
    }

    public int GetLevelWidth(uint level) => GetLevelDimension(Width, level);

    public int GetLevelHeight(uint level) => GetLevelDimension(Height, level);

    public int GetLevelDepth(uint level) => GetLevelDimension(Depth, level);

    public uint GetComponents()
    {
        return Format switch
        {
            TextureFormat.Depth24Stencil8 => 0u,
            TextureFormat.R8 => 1u,
            TextureFormat.RG16F => 4u,
            TextureFormat.RGB8 => 3u,
            TextureFormat.RGBA8 => 4u,
            TextureFormat.RGBA16F => 8u,
            _ => 0u
        };
    }

    public uint GetDataSize(int width, int height) => GetDataSize(width, height, 1);

    public uint GetDataSize(int width, int height, int depth)
    {
        return (uint)Math.Max(width, 0) * (uint)Math.Max(height, 0) * (uint)Math.Max(depth, 0) * GetComponents();
    }

    public uint GetRowDataSize(int width)
    {
        return (uint)Math.Max(width, 0) * GetComponents();
    }

    protected void SetDimensions(int width, int height, int depth, TextureFormat format)
    {
        Width = Math.Max(width, 0);
        Height = Math.Max(height, 0);
        Depth = Math.Max(depth, 1);
        Format = format;
        Levels = Levels == 0 ? CalculateFullMipChainLength(Math.Max(Width, Math.Max(Height, Depth))) : Math.Max(Levels, 1u);
    }

    protected void RefreshMemoryUse(uint totalBytes)
    {
        MemoryUse = totalBytes;
    }

    private static int GetLevelDimension(int size, uint level)
    {
        if (size <= 0)
            return 0;

        var value = size;
        for (var i = 0u; i < level && value > 1; ++i)
            value = Math.Max(value / 2, 1);
        return value;
    }

    private static uint CalculateFullMipChainLength(int largestDimension)
    {
        if (largestDimension <= 0)
            return 1u;

        var levels = 1u;
        while (largestDimension > 1)
        {
            largestDimension = Math.Max(largestDimension / 2, 1);
            ++levels;
        }

        return levels;
    }
}
