using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class RawTextureTests
{
    private readonly HeadlessEngineFixture fixture;

    public RawTextureTests(HeadlessEngineFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public void Raw_texture_creates_managed_2d_texture_and_deduces_full_mip_chain()
    {
        var rawTexture = new RawTexture(new RawTextureParams(
            TextureType.Texture2D,
            TextureFormat.RGBA8,
            TextureFlag.BindRenderTarget,
            new IntVector3(16, 8, 1)));

        var texture = Assert.IsType<Texture2D>(rawTexture.GetTexture());
        var parameters = rawTexture.GetParams();

        Assert.Equal(16, texture.Width);
        Assert.Equal(8, texture.Height);
        Assert.Equal((uint)5, texture.Levels);
        Assert.Equal((uint)5, parameters.NumLevels);
        Assert.Equal((uint)5, parameters.NumLevelsRtv);
        Assert.Equal(TextureFormat.RGBA8, parameters.Format);
        Assert.Equal(TextureType.Texture2D, parameters.Type);
    }

    [Fact]
    public void Raw_texture_applies_sampler_state_to_wrapped_managed_texture()
    {
        var rawTexture = new RawTexture(new RawTextureParams(
            TextureType.Texture2D,
            TextureFormat.RGBA8,
            TextureFlag.None,
            new IntVector3(4, 4, 1)));
        var samplerState = SamplerStateDesc.Bilinear(TextureAddressMode.Clamp);
        samplerState[TextureCoordinate.W] = TextureAddressMode.Mirror;
        samplerState.Anisotropy = 6;
        samplerState.ShadowCompare = true;

        rawTexture.SetSamplerStateDesc(samplerState);

        var texture = Assert.IsType<Texture2D>(rawTexture.GetTexture());
        Assert.Equal(TextureFilterMode.Bilinear, texture.GetFilterMode());
        Assert.Equal(TextureAddressMode.Clamp, texture.GetAddressMode(TextureCoordinate.U));
        Assert.Equal(TextureAddressMode.Clamp, texture.GetAddressMode(TextureCoordinate.V));
        Assert.Equal(TextureAddressMode.Mirror, texture.GetAddressMode(TextureCoordinate.W));
        Assert.Equal((uint)6, texture.GetAnisotropy());
        Assert.True(texture.GetShadowCompare());
    }

    [Fact]
    public void Raw_texture_creates_cube_and_array_wrappers_from_params()
    {
        var cubeTexture = new RawTexture(new RawTextureParams(
            TextureType.TextureCube,
            TextureFormat.RGBA8,
            TextureFlag.BindRenderTarget,
            new IntVector3(32, 16, 1)));
        var arrayTexture = new RawTexture(new RawTextureParams(
            TextureType.Texture2DArray,
            TextureFormat.Depth24Stencil8,
            TextureFlag.BindDepthStencil,
            new IntVector3(8, 4, 1),
            ArraySize: 3u));

        var cube = Assert.IsType<TextureCube>(cubeTexture.GetTexture());
        var array = Assert.IsType<Texture2DArray>(arrayTexture.GetTexture());

        Assert.Equal(32, cube.Width);
        Assert.Equal(32, cube.Height);
        Assert.Equal((uint)3, arrayTexture.GetParams().ArraySize);
        Assert.Equal((uint)3, array.Layers);
        Assert.Equal(TextureFormat.Depth24Stencil8, array.Format);
    }

    [Fact]
    public void Render_pool_recycles_transient_textures_by_params()
    {
        var renderPool = fixture.RenderDevice.GetRenderPool();
        var parameters = new RawTextureParams(
            TextureType.Texture2D,
            TextureFormat.RGBA8,
            TextureFlag.BindRenderTarget,
            new IntVector3(64, 64, 1));

        var first = renderPool.GetTexture(parameters);
        var second = renderPool.GetTexture(parameters);

        Assert.NotSame(first, second);

        renderPool.RecycleTextures();

        var third = renderPool.GetTexture(parameters);
        Assert.Same(first, third);
    }

    [Fact]
    public void Render_pool_reuses_persistent_textures_for_matching_persistence_keys()
    {
        var renderPool = fixture.RenderDevice.GetRenderPool();
        var parameters = new RawTextureParams(
            TextureType.Texture2D,
            TextureFormat.RGBA8,
            TextureFlag.BindRenderTarget,
            new IntVector3(32, 32, 1));

        var first = renderPool.GetTexture(parameters, persistenceKey: (nint)1);
        var second = renderPool.GetTexture(parameters, persistenceKey: (nint)1);
        var third = renderPool.GetTexture(parameters, persistenceKey: (nint)2);

        Assert.Same(first, second);
        Assert.NotSame(first, third);
    }

    [Fact]
    public void Render_target_view_accepts_raw_texture_inputs()
    {
        var rawTexture = new RawTexture(new RawTextureParams(
            TextureType.Texture2D,
            TextureFormat.RGBA8,
            TextureFlag.BindRenderTarget,
            new IntVector3(16, 16, 1)));

        var renderTargetView = RenderTargetView.Texture(rawTexture);
        var readOnlyDepthView = RenderTargetView.ReadOnlyDepth(rawTexture);

        Assert.Same(rawTexture.GetTexture(), renderTargetView.RenderTexture);
        Assert.Same(rawTexture.GetTexture(), readOnlyDepthView.RenderTexture);
    }
}
