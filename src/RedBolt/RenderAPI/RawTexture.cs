namespace RedBolt;

public sealed class RawTexture : DeviceObject
{
    private RawTextureParams parameters;
    private SamplerStateDesc samplerStateDesc = new();
    private Texture? texture;

    public RawTexture()
    {
    }

    public RawTexture(RawTextureParams parameters)
    {
        if (!Create(parameters))
            throw new InvalidOperationException($"Failed to create raw texture for type '{parameters.Type}' and format '{parameters.Format}'.");
    }

    public bool Create(RawTextureParams parameters)
    {
        if (!TryCreateManagedTexture(parameters, out var createdTexture, out var finalizedParameters))
        {
            texture = null;
            this.parameters = default;
            return false;
        }

        texture = createdTexture;
        this.parameters = finalizedParameters;
        ApplySamplerState();
        SetDebugName($"RawTexture #{parameters.GetHashCode():X8}");
        return true;
    }

    public void SetSamplerStateDesc(SamplerStateDesc desc)
    {
        samplerStateDesc = desc ?? throw new ArgumentNullException(nameof(desc));
        ApplySamplerState();
    }

    public RawTextureParams GetParams() => parameters;

    public SamplerStateDesc GetSamplerStateDesc() => samplerStateDesc;

    public Texture? GetTexture() => texture;

    public void MarkDirty()
    {
        if (texture is null)
            return;

        RenderTargetView.Texture(this).MarkDirty();
    }

    public override void Destroy()
    {
        base.Destroy();
        texture = null;
    }

    private void ApplySamplerState()
    {
        if (texture is null)
            return;

        texture.SetFilterMode(samplerStateDesc.FilterMode);
        texture.SetAddressMode(TextureCoordinate.U, samplerStateDesc[TextureCoordinate.U]);
        texture.SetAddressMode(TextureCoordinate.V, samplerStateDesc[TextureCoordinate.V]);
        texture.SetAddressMode(TextureCoordinate.W, samplerStateDesc[TextureCoordinate.W]);
        texture.SetAnisotropy(samplerStateDesc.Anisotropy);
        texture.SetShadowCompare(samplerStateDesc.ShadowCompare);
    }

    private static bool TryCreateManagedTexture(RawTextureParams parameters, out Texture createdTexture, out RawTextureParams finalizedParameters)
    {
        createdTexture = null!;
        finalizedParameters = default;

        if (parameters.Format == TextureFormat.Unknown)
            return false;

        var normalizedArraySize = Math.Max(parameters.ArraySize, 1u);
        var normalizedMultiSample = Math.Max(parameters.MultiSample, 1u);
        var normalizedSize = new IntVector3(
            Math.Max(parameters.Size.X, 1),
            Math.Max(parameters.Size.Y, 1),
            Math.Max(parameters.Size.Z, 1));

        Texture textureCandidate;
        switch (parameters.Type)
        {
            case TextureType.Texture2D:
            {
                var texture2D = new Texture2D($"RawTexture2D.{Guid.NewGuid():N}");
                texture2D.SetNumLevels(parameters.NumLevels);
                if (!texture2D.SetSize(normalizedSize.X, normalizedSize.Y, parameters.Format, (int)normalizedMultiSample,
                    parameters.Flags.HasFlag(TextureFlag.NoMultiSampledAutoResolve)))
                    return false;
                textureCandidate = texture2D;
                break;
            }

            case TextureType.TextureCube:
            {
                var textureCube = new TextureCube($"RawTextureCube.{Guid.NewGuid():N}");
                textureCube.SetNumLevels(parameters.NumLevels);
                var edge = Math.Max(normalizedSize.X, normalizedSize.Y);
                if (!textureCube.SetSize(edge, parameters.Format, (int)normalizedMultiSample,
                    parameters.Flags.HasFlag(TextureFlag.NoMultiSampledAutoResolve)))
                    return false;
                normalizedSize = new IntVector3(edge, edge, 1);
                textureCandidate = textureCube;
                break;
            }

            case TextureType.Texture2DArray:
            {
                var textureArray = new Texture2DArray($"RawTextureArray.{Guid.NewGuid():N}");
                textureArray.SetNumLevels(parameters.NumLevels);
                if (!textureArray.SetLayers(normalizedArraySize, normalizedSize.X, normalizedSize.Y, parameters.Format, (int)normalizedMultiSample,
                    parameters.Flags.HasFlag(TextureFlag.NoMultiSampledAutoResolve)))
                    return false;
                textureCandidate = textureArray;
                break;
            }

            case TextureType.Texture3D:
            default:
                return false;
        }

        createdTexture = textureCandidate;
        finalizedParameters = new RawTextureParams(
            parameters.Type,
            parameters.Format,
            parameters.Flags,
            new IntVector3(textureCandidate.Width, textureCandidate.Height, textureCandidate.Depth),
            parameters.Type == TextureType.Texture2DArray ? (uint)textureCandidate.Depth : normalizedArraySize,
            textureCandidate.Levels,
            (uint)textureCandidate.MultiSample,
            CalculateNumLevelsRtv(textureCandidate, parameters));
        return true;
    }

    private static uint CalculateNumLevelsRtv(Texture texture, RawTextureParams parameters)
    {
        if (parameters.NumLevelsRtv != 0u)
            return parameters.NumLevelsRtv;

        return texture.MultiSample > 1 && !parameters.Flags.HasFlag(TextureFlag.NoMultiSampledAutoResolve)
            ? 1u
            : texture.Levels;
    }
}
