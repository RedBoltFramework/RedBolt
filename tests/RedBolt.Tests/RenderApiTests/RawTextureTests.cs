using RedBolt;
using RedBolt.Bindings.Diligent;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class RawTextureTests
{
    [Fact]
    public void Texture2D_image_upload_and_readback_use_raw_texture()
    {
        WithInitializedGraphics(() =>
        {
            var image = new Image("Textures/RawTextureImage");
            var imageData = Enumerable.Range(0, 16).Select(value => (byte)(value * 3)).ToArray();
            Assert.True(image.SetSize(2, 2, 4u));
            image.SetData(imageData);

            var texture = new Texture2D("Textures/RawTexture2D");
            texture.SetNumLevels(1u);
            Assert.True(texture.SetData(image));

            var readback = new byte[16];
            Assert.True(texture.GetData(0u, readback));
            Assert.Equal(imageData, readback);
            Assert.False(texture.GetData(0u, new byte[15]));

            var output = texture.GetImage();
            Assert.NotNull(output);
            Assert.Equal(2, output.Width);
            Assert.Equal(2, output.Height);
            Assert.Equal(imageData, output.GetData());
        });
    }

    private readonly HeadlessEngineFixture _fixture;

    public RawTextureTests(HeadlessEngineFixture fixture)
    {
        this._fixture = fixture;
    }

    [Fact]
    public void Raw_texture_value_types_and_index_types_match_port_mappings()
    {
        Assert.True(typeof(RawTextureParams).IsValueType);
        Assert.True(typeof(RawTextureUAVKey).IsValueType);
        Assert.True(typeof(RawTextureHandles).IsValueType);
        Assert.Equal(typeof(int), typeof(RawTextureParams).GetProperty(nameof(RawTextureParams.ArraySize))!.PropertyType);
        Assert.Equal(typeof(int), typeof(RawTextureParams).GetProperty(nameof(RawTextureParams.NumLevels))!.PropertyType);
        Assert.Equal(typeof(int), typeof(RawTextureParams).GetProperty(nameof(RawTextureParams.MultiSample))!.PropertyType);
        Assert.Equal(typeof(int), typeof(RawTextureParams).GetProperty(nameof(RawTextureParams.NumLevelsRtv))!.PropertyType);
        Assert.Equal(typeof(int), typeof(RawTextureUAVKey).GetProperty(nameof(RawTextureUAVKey.FirstSlice))!.PropertyType);
        Assert.Equal(typeof(int), typeof(RawTextureUAVKey).GetProperty(nameof(RawTextureUAVKey.FirstLevel))!.PropertyType);
        Assert.Equal(typeof(int), typeof(RawTextureUAVKey).GetProperty(nameof(RawTextureUAVKey.NumSlices))!.PropertyType);
        Assert.Equal(typeof(int), typeof(RawTextureUAVKey).GetProperty(nameof(RawTextureUAVKey.NumLevels))!.PropertyType);
    }

    [Fact]
    public void Raw_texture_creates_managed_2d_texture_and_deduces_full_mip_chain()
    {
        var rawTexture = new RawTexture(new RawTextureParams(
            TextureType.Texture2D,
            TextureFormat.RGBA8,
            TextureFlag.BindRenderTarget,
            new Vector3i(16, 8, 1)));

        var texture = Assert.IsType<Texture2D>(rawTexture.GetTexture());
        var parameters = rawTexture.GetParams();

        Assert.Equal(16, texture.Width);
        Assert.Equal(8, texture.Height);
        Assert.Equal((uint)5, texture.Levels);
        Assert.Equal(5, parameters.NumLevels);
        Assert.Equal(5, parameters.NumLevelsRtv);
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
            new Vector3i(4, 4, 1)));
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
    public void Raw_texture_copies_sampler_state_on_assignment_and_return()
    {
        using var rawTexture = new RawTexture(new RawTextureParams(
            TextureType.Texture2D,
            TextureFormat.RGBA8,
            TextureFlag.None,
            new Vector3i(4, 4, 1)));
        var source = SamplerStateDesc.Bilinear(TextureAddressMode.Clamp);

        rawTexture.SetSamplerStateDesc(source);
        source[TextureCoordinate.U] = TextureAddressMode.Mirror;
        var returned = rawTexture.GetSamplerStateDesc();
        returned[TextureCoordinate.V] = TextureAddressMode.Mirror;

        Assert.Equal(TextureAddressMode.Clamp, rawTexture.GetSamplerStateDesc()[TextureCoordinate.U]);
        Assert.Equal(TextureAddressMode.Clamp, rawTexture.GetSamplerStateDesc()[TextureCoordinate.V]);
    }

    [Fact]
    public void Raw_texture_preserves_srgb_color_space()
    {
        var rawTexture = new RawTexture(new RawTextureParams(
            TextureType.Texture2D,
            TextureFormat.RGBA8,
            TextureFlag.None,
            new Vector3i(4, 4, 1),
            SRGB: true));

        var texture = Assert.IsType<Texture2D>(rawTexture.GetTexture());

        Assert.True(texture.GetSRGB());
        Assert.True(rawTexture.GetParams().SRGB);
    }

    [Fact]
    public void Raw_texture_creates_cube_and_array_wrappers_from_params()
    {
        var cubeTexture = new RawTexture(new RawTextureParams(
            TextureType.TextureCube,
            TextureFormat.RGBA8,
            TextureFlag.BindRenderTarget,
            new Vector3i(32, 16, 1)));
        var arrayTexture = new RawTexture(new RawTextureParams(
            TextureType.Texture2DArray,
            TextureFormat.Depth24Stencil8,
            TextureFlag.BindDepthStencil,
            new Vector3i(8, 4, 1),
            ArraySize: 3));

        var cube = Assert.IsType<TextureCube>(cubeTexture.GetTexture());
        var array = Assert.IsType<Texture2DArray>(arrayTexture.GetTexture());

        Assert.Equal(32, cube.Width);
        Assert.Equal(32, cube.Height);
        Assert.Equal(3, arrayTexture.GetParams().ArraySize);
        Assert.Equal((uint)3, array.Layers);
        Assert.Equal(TextureFormat.Depth24Stencil8, array.Format);
    }

    [Fact]
    public void Raw_texture_rejects_invalid_dimensions_bindings_and_multisampling()
    {
        var texture = new TestRawTexture();

        Assert.False(texture.CreateForTest(new RawTextureParams(
            TextureType.Texture2D, TextureFormat.RGBA8, TextureFlag.None, new Vector3i(0, 4, 1))));
        Assert.False(texture.CreateForTest(new RawTextureParams(
            TextureType.Texture2D, TextureFormat.RGBA8,
            TextureFlag.BindRenderTarget | TextureFlag.BindDepthStencil, new Vector3i(4, 4, 1))));
        Assert.False(texture.CreateForTest(new RawTextureParams(
            TextureType.Texture2D, TextureFormat.RGBA8, TextureFlag.None, new Vector3i(4, 4, 1), MultiSample: 4)));
    }

    private sealed class TestRawTexture : RawTexture
    {
        public bool CreateForTest(RawTextureParams parameters) => Create(parameters);
    }

    [Fact]
    public void Raw_texture_normalizes_dimensions_and_mip_rules_like_rbfx()
    {
        var cube = new RawTexture(new RawTextureParams(
            TextureType.TextureCube, TextureFormat.RGBA8, TextureFlag.BindRenderTarget,
            new Vector3i(16, 8, 9), ArraySize: 2));
        var depth = new RawTexture(new RawTextureParams(
            TextureType.Texture2D, TextureFormat.Depth24Stencil8, TextureFlag.BindDepthStencil,
            new Vector3i(16, 8, 1), NumLevels: 5));

        Assert.Equal(new Vector3i(16, 16, 1), cube.GetParams().Size);
        Assert.Equal(6, cube.GetParams().ArraySize);
        Assert.Equal(1, depth.GetParams().NumLevels);
        Assert.Equal(1, depth.GetParams().NumLevelsRtv);
    }

    [Fact]
    public void Multisampled_render_target_marks_resolve_and_mip_levels_dirty()
    {
        var texture = new RawTexture(new RawTextureParams(
            TextureType.Texture2D, TextureFormat.RGBA8, TextureFlag.BindRenderTarget,
            new Vector3i(16, 16, 1), MultiSample: 4));

        texture.MarkDirty();

        Assert.True(texture.GetLevelsDirty());
        Assert.True(texture.GetResolveDirty());
    }

    [Fact]
    public void Render_pool_recycles_transient_textures_by_params()
    {
        var renderPool = _fixture.RenderDevice.GetRenderPool();
        var parameters = new RawTextureParams(
            TextureType.Texture2D,
            TextureFormat.RGBA8,
            TextureFlag.BindRenderTarget,
            new Vector3i(64, 64, 1));

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
        var renderPool = _fixture.RenderDevice.GetRenderPool();
        var parameters = new RawTextureParams(
            TextureType.Texture2D,
            TextureFormat.RGBA8,
            TextureFlag.BindRenderTarget,
            new Vector3i(32, 32, 1));

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
            new Vector3i(16, 16, 1)));
        var rawDepthTexture = new RawTexture(new RawTextureParams(
            TextureType.Texture2D,
            TextureFormat.Depth24Stencil8,
            TextureFlag.BindDepthStencil,
            new Vector3i(16, 16, 1)));

        var renderTargetView = RenderTargetView.Texture(rawTexture);
        var readOnlyDepthView = RenderTargetView.ReadOnlyDepth(rawDepthTexture);

        Assert.Same(rawTexture.GetTexture(), renderTargetView.RenderTexture);
        Assert.Same(rawDepthTexture.GetTexture(), readOnlyDepthView.RenderTexture);
        Assert.Throws<ArgumentException>(() => RenderTargetView.ReadOnlyDepth(rawTexture));
    }

    [Fact]
    public void Raw_texture_creates_native_handle_when_graphics_is_initialized()
    {
        WithInitializedGraphics(() =>
        {
            var rawTexture = new RawTexture(new RawTextureParams(
                TextureType.Texture2D,
                TextureFormat.RGBA8,
                TextureFlag.BindRenderTarget,
                new Vector3i(32, 16, 1)));

            var handle = rawTexture.GetHandle();

            Assert.NotNull(handle);
            Assert.False(handle!.IsNull);

            var desc = handle.GetDesc();
            Assert.Equal((uint)32, desc.Width);
            Assert.Equal((uint)16, desc.Height);
        });
    }

    [Fact]
    public void Raw_depth_texture_exposes_writable_and_read_only_depth_views()
    {
        WithInitializedGraphics(() =>
        {
            var rawTexture = new RawTexture(new RawTextureParams(
                TextureType.Texture2D,
                TextureFormat.Depth32,
                TextureFlag.BindDepthStencil,
                new Vector3i(32, 16, 1)));

            var writableView = Assert.IsAssignableFrom<ITextureView>(rawTexture.GetDepthStencilView());
            var readOnlyView = Assert.IsAssignableFrom<ITextureView>(rawTexture.GetReadOnlyDepthStencilView());

            Assert.Equal(TextureViewType.TextureViewDepthStencil, writableView.GetDesc().ViewType);
            Assert.Equal(TextureViewType.TextureViewReadOnlyDepthStencil, readOnlyView.GetDesc().ViewType);
            Assert.Same(writableView, RenderTargetView.Texture(rawTexture).GetView());
            Assert.Same(readOnlyView, RenderTargetView.ReadOnlyDepth(rawTexture).GetView());
        });
    }

    [Fact]
    public void Raw_texture_creates_and_caches_single_mip_uav_when_graphics_is_initialized()
    {
        WithInitializedGraphics(() =>
        {
            var rawTexture = new RawTexture(new RawTextureParams(
                TextureType.Texture2D,
                TextureFormat.RGBA8,
                TextureFlag.BindRenderTarget | TextureFlag.BindUnorderedAccess,
                new Vector3i(16, 16, 1)));
            var key = new RawTextureUAVKey(NumLevels: 1);

            var firstView = rawTexture.CreateUAV(key);
            var secondView = rawTexture.GetUAV(key);

            Assert.NotNull(firstView);
            Assert.Same(firstView, secondView);

            var desc = firstView!.GetDesc();
            Assert.Equal(TextureViewType.TextureViewUnorderedAccess, desc.ViewType);
            Assert.Equal(ResourceDimension.ResourceDimTex2d, desc.TextureDim);
            Assert.Equal((uint)0, desc.FirstArraySlice);
            Assert.Equal((uint)0, desc.MostDetailedMip);
            Assert.Equal((uint)1, desc.NumMipLevels);
            Assert.Equal(UavAccessFlag.UavAccessFlagReadWrite, desc.AccessFlags);
        });
    }

    [Fact]
    public void Raw_texture_uav_key_default_constructor_matches_cpp_member_initializers()
    {
        var key = new RawTextureUAVKey();

        Assert.True(key.CanWrite);
        Assert.True(key.CanRead);
        Assert.Equal(0, key.FirstSlice);
        Assert.Equal(0, key.FirstLevel);
        Assert.Equal(0, key.NumSlices);
        Assert.Equal(0, key.NumLevels);
    }

    [Fact]
    public void Raw_texture_normalizes_uav_key_ranges_before_caching_view()
    {
        WithInitializedGraphics(() =>
        {
            var rawTexture = new RawTexture(new RawTextureParams(
                TextureType.Texture2DArray,
                TextureFormat.RGBA8,
                TextureFlag.BindRenderTarget | TextureFlag.BindUnorderedAccess,
                new Vector3i(8, 8, 1),
                ArraySize: 4,
                NumLevels: 3));
            var requestedKey = new RawTextureUAVKey(CanWrite: true, CanRead: false, FirstLevel: 1);
            var normalizedKey = requestedKey with { NumSlices = 4, NumLevels = 2 };

            var firstView = rawTexture.CreateUAV(requestedKey);
            var secondView = rawTexture.GetUAV(normalizedKey);

            Assert.NotNull(firstView);
            Assert.Same(firstView, secondView);

            var desc = firstView!.GetDesc();
            Assert.Equal(TextureViewType.TextureViewUnorderedAccess, desc.ViewType);
            Assert.Equal(ResourceDimension.ResourceDimTex2dArray, desc.TextureDim);
            Assert.Equal((uint)0, desc.FirstArraySlice);
            Assert.Equal((uint)4, desc.NumArraySlices);
            Assert.Equal((uint)1, desc.MostDetailedMip);
            Assert.Equal((uint)2, desc.NumMipLevels);
            Assert.Equal(UavAccessFlag.UavAccessFlagWrite, desc.AccessFlags);
        });
    }

    [Fact]
    public void Raw_texture_does_not_create_uav_without_unordered_access_flag()
    {
        WithInitializedGraphics(() =>
        {
            var rawTexture = new RawTexture(new RawTextureParams(
                TextureType.Texture2D,
                TextureFormat.RGBA8,
                TextureFlag.BindRenderTarget,
                new Vector3i(16, 16, 1)));

            Assert.Null(rawTexture.CreateUAV(new RawTextureUAVKey()));
            Assert.Null(rawTexture.GetUAV(new RawTextureUAVKey()));
        });
    }

    [Fact]
    public void Raw_texture_updates_and_reads_native_texture_data()
    {
        WithInitializedGraphics(() =>
        {
            var rawTexture = new RawTexture(new RawTextureParams(
                TextureType.Texture2D, TextureFormat.R8, TextureFlag.None, new Vector3i(4, 3, 1), NumLevels: 1));
            byte[] source = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12];
            var destination = new byte[source.Length];

            rawTexture.Update(0, Vector3i.Zero, new Vector3i(4, 3, 1), 0, source, rowStride: 4);

            Assert.True(rawTexture.Read(0, 0, destination));
            Assert.Equal(source, destination);
        });
    }

    [Fact]
    public void Raw_texture_update_hot_path_does_not_allocate_managed_memory()
    {
        WithInitializedGraphics(() =>
        {
            var rawTexture = new RawTexture(new RawTextureParams(
                TextureType.Texture2D, TextureFormat.RGBA8, TextureFlag.None, new Vector3i(4, 4, 1), NumLevels: 1));
            var source = new byte[4 * 4 * 4];
            rawTexture.Update(0, Vector3i.Zero, new Vector3i(4, 4, 1), 0, source);

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 32; ++i)
                rawTexture.Update(0, Vector3i.Zero, new Vector3i(4, 4, 1), 0, source);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.Equal(0, allocated);
        });
    }

    [Fact]
    public void Raw_texture_wraps_existing_native_texture_handle()
    {
        WithInitializedGraphics(() =>
        {
            var source = new RawTexture(new RawTextureParams(
                TextureType.Texture2D, TextureFormat.RGBA8, TextureFlag.BindRenderTarget,
                new Vector3i(8, 4, 1), NumLevels: 1));
            var wrapped = new TestRawTexture();

            Assert.True(wrapped.CreateFromHandle(source.GetHandle()!, TextureFormat.Unknown, 1));
            Assert.Same(source.GetHandle(), wrapped.GetHandle());
            Assert.Equal(new Vector3i(8, 4, 1), wrapped.GetParams().Size);
            Assert.Equal(TextureFlag.BindRenderTarget | TextureFlag.NoMultiSampledAutoResolve,
                wrapped.GetParams().Flags);
            Assert.NotNull(wrapped.GetShaderResourceView());
            Assert.NotNull(wrapped.GetRenderTargetView());

            wrapped.Destroy();
            Assert.False(source.GetHandle()!.IsNull);
        });
    }

    private void WithInitializedGraphics(Action action)
    {
        var parameters = new EngineParameters
        {
            ApplicationName = "RedBolt.Tests.RawTextureTests",
            Headless = false,
            WindowWidth = 64,
            WindowHeight = 64
        };

        RbfxTestData.AddResourceDirs(_fixture.Resources);
        _fixture.Input.Initialize(parameters);
        _fixture.Graphics.Initialize(parameters, _fixture.Input);
        _fixture.RenderDevice.RestoreDeviceState();
        try
        {
            action();
        }
        finally
        {
            _fixture.RenderDevice.InvalidateDeviceState();
            _fixture.Graphics.Shutdown();
            _fixture.Input.Shutdown();
            RbfxTestData.RemoveResourceDirs(_fixture.Resources);
        }
    }
}
