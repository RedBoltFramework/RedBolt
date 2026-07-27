using RedBolt;
using RedBolt.Bindings.Diligent;

using RedBolt.Container;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.RenderAPI;
using RedBolt.Resource;
namespace RedBolt.Tests;
using TextureAddressMode = global::RedBolt.RenderAPI.TextureAddressMode;


[Collection(nameof(GraphicsEngineCollection))]
public sealed class RawTextureTests
{
    [Fact]
    public void Texture2D_image_upload_and_readback_use_raw_texture()
    {
        WithInitializedGraphics(() =>
        {
            var image = new Image();
            var imageData = Enumerable.Range(0, 16).Select(value => (byte)(value * 3)).ToArray();
            Assert.True(image.SetSize(2, 2, 4));
            image.SetData(imageData);

            var texture = new Texture2D { Name = "Textures/RawTexture2D" };
            texture.SetNumLevels(1);
            Assert.True(texture.SetData(image));

            var readback = new byte[16];
            Assert.True(texture.GetData(0, readback));
            Assert.Equal(imageData, readback);
            Assert.False(texture.GetData(0, new byte[15]));

            var output = texture.GetImage();
            Assert.NotNull(output);
            Assert.Equal(2, output.Width);
            Assert.Equal(2, output.Height);
            Assert.Equal(imageData, output.GetData());
        });
    }

    private readonly GraphicsEngineFixture _fixture;

    public RawTextureTests(GraphicsEngineFixture fixture)
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
    public void Raw_texture_deduces_full_mip_chain()
    {
        WithInitializedGraphics(() =>
        {
            var rawTexture = new RawTexture(new RawTextureParams(
                TextureType.Texture2D,
                TextureFormat.TexFormatRgba8Unorm,
                TextureFlag.BindRenderTarget,
                new Vector3i(16, 8, 1)));

            var parameters = rawTexture.GetParams();

            Assert.True(rawTexture.GetHandles().IsValid());
            Assert.Equal(new Vector3i(16, 8, 1), parameters.Size);
            Assert.Equal(5, parameters.NumLevels);
            Assert.Equal(5, parameters.NumLevelsRtv);
            Assert.Equal(TextureFormat.TexFormatRgba8Unorm, parameters.Format);
            Assert.Equal(TextureType.Texture2D, parameters.Type);
        });
    }

    [Fact]
    public void Raw_texture_copies_sampler_state_on_assignment_and_return()
    {
        WithInitializedGraphics(() =>
        {
            using var rawTexture = new RawTexture(new RawTextureParams(
                TextureType.Texture2D,
                TextureFormat.TexFormatRgba8Unorm,
                TextureFlag.None,
                new Vector3i(4, 4, 1)));
            var source = SamplerStateDesc.Bilinear(TextureAddressMode.Clamp);

            rawTexture.SetSamplerStateDesc(source);
            source[TextureCoordinate.U] = TextureAddressMode.Mirror;
            var returned = rawTexture.GetSamplerStateDesc();
            returned[TextureCoordinate.V] = TextureAddressMode.Mirror;

            Assert.Equal(TextureAddressMode.Clamp, rawTexture.GetSamplerStateDesc()[TextureCoordinate.U]);
            Assert.Equal(TextureAddressMode.Clamp, rawTexture.GetSamplerStateDesc()[TextureCoordinate.V]);
        });
    }

    [Fact]
    public void Raw_texture_preserves_srgb_texture_format()
    {
        WithInitializedGraphics(() =>
        {
            var rawTexture = new RawTexture(new RawTextureParams(
                TextureType.Texture2D,
                TextureFormat.TexFormatRgba8UnormSrgb,
                TextureFlag.None,
                new Vector3i(4, 4, 1)));

            Assert.Equal(TextureFormat.TexFormatRgba8UnormSrgb, rawTexture.GetParams().Format);
        });
    }

    [Fact]
    public void Raw_texture_normalizes_cube_and_array_params()
    {
        WithInitializedGraphics(() =>
        {
            var cubeTexture = new RawTexture(new RawTextureParams(
                TextureType.TextureCube,
                TextureFormat.TexFormatRgba8Unorm,
                TextureFlag.BindRenderTarget,
                new Vector3i(32, 16, 1)));
            var arrayTexture = new RawTexture(new RawTextureParams(
                TextureType.Texture2DArray,
                TextureFormat.TexFormatD24UnormS8Uint,
                TextureFlag.BindDepthStencil,
                new Vector3i(8, 4, 1),
                ArraySize: 3));

            Assert.Equal(new Vector3i(32, 32, 1), cubeTexture.GetParams().Size);
            Assert.Equal(6, cubeTexture.GetParams().ArraySize);
            Assert.Equal(3, arrayTexture.GetParams().ArraySize);
            Assert.Contains(arrayTexture.GetParams().Format,
                new[] { TextureFormat.TexFormatD24UnormS8Uint, TextureFormat.TexFormatD32FloatS8x24Uint });
        });
    }

    [Fact]
    public void Raw_texture_rejects_invalid_dimensions_bindings_and_multisampling()
    {
        WithInitializedGraphics(() =>
        {
            var texture = new TestRawTexture();

            Assert.False(texture.CreateForTest(new RawTextureParams(
                TextureType.Texture2D, TextureFormat.TexFormatRgba8Unorm, TextureFlag.None, new Vector3i(0, 4, 1))));
            Assert.False(texture.CreateForTest(new RawTextureParams(
                TextureType.Texture2D, TextureFormat.TexFormatRgba8Unorm,
                TextureFlag.BindRenderTarget | TextureFlag.BindDepthStencil, new Vector3i(4, 4, 1))));
            Assert.False(texture.CreateForTest(new RawTextureParams(
                TextureType.Texture2D, TextureFormat.TexFormatRgba8Unorm, TextureFlag.None, new Vector3i(4, 4, 1), MultiSample: 4)));
        });
    }

    private sealed class TestRawTexture : RawTexture
    {
        public bool CreateForTest(RawTextureParams parameters) => Create(parameters);
    }

    [Fact]
    public void Raw_texture_normalizes_dimensions_and_mip_rules_like_rbfx()
    {
        WithInitializedGraphics(() =>
        {
            var cube = new RawTexture(new RawTextureParams(
                TextureType.TextureCube, TextureFormat.TexFormatRgba8Unorm, TextureFlag.BindRenderTarget,
                new Vector3i(16, 8, 9), ArraySize: 2));
            var depth = new RawTexture(new RawTextureParams(
                TextureType.Texture2D, TextureFormat.TexFormatD24UnormS8Uint, TextureFlag.BindDepthStencil,
                new Vector3i(16, 8, 1), NumLevels: 5));

            Assert.Equal(new Vector3i(16, 16, 1), cube.GetParams().Size);
            Assert.Equal(6, cube.GetParams().ArraySize);
            Assert.Equal(1, depth.GetParams().NumLevels);
            Assert.Equal(1, depth.GetParams().NumLevelsRtv);
        });
    }

    [Fact]
    public void Multisampled_render_target_marks_resolve_and_mip_levels_dirty()
    {
        WithInitializedGraphics(() =>
        {
            var multiSample = _fixture.Graphics.GetSupportedMultiSample(TextureFormat.TexFormatRgba8Unorm, 4);
            if (multiSample <= 1)
                return;
            var texture = new RawTexture(new RawTextureParams(
                TextureType.Texture2D, TextureFormat.TexFormatRgba8Unorm, TextureFlag.BindRenderTarget,
                new Vector3i(16, 16, 1), MultiSample: multiSample));

            texture.MarkDirty();

            Assert.True(texture.GetLevelsDirty());
            Assert.True(texture.GetResolveDirty());
        });
    }

    [Fact]
    public void Render_pool_recycles_transient_textures_by_params()
    {
        WithInitializedGraphics(() =>
        {
            var renderPool = _fixture.RenderDevice.GetRenderPool();
            var parameters = new RawTextureParams(
                TextureType.Texture2D,
                TextureFormat.TexFormatRgba8Unorm,
                TextureFlag.BindRenderTarget,
                new Vector3i(64, 64, 1));

            var first = renderPool.GetTexture(parameters);
            var second = renderPool.GetTexture(parameters);

            Assert.NotSame(first, second);

            renderPool.RecycleTextures();

            var third = renderPool.GetTexture(parameters);
            Assert.Same(first, third);
        });
    }

    [Fact]
    public void Render_pool_reuses_persistent_textures_for_matching_persistence_keys()
    {
        WithInitializedGraphics(() =>
        {
            var renderPool = _fixture.RenderDevice.GetRenderPool();
            var parameters = new RawTextureParams(
                TextureType.Texture2D,
                TextureFormat.TexFormatRgba8Unorm,
                TextureFlag.BindRenderTarget,
                new Vector3i(32, 32, 1));

            var first = renderPool.GetTexture(parameters, persistenceKey: (nint)1);
            var second = renderPool.GetTexture(parameters, persistenceKey: (nint)1);
            var third = renderPool.GetTexture(parameters, persistenceKey: (nint)2);

            Assert.Same(first, second);
            Assert.NotSame(first, third);
        });
    }

    [Fact]
    public void Render_target_view_accepts_raw_texture_inputs()
    {
        WithInitializedGraphics(() =>
        {
            var rawTexture = new RawTexture(new RawTextureParams(
                TextureType.Texture2D,
                TextureFormat.TexFormatRgba8Unorm,
                TextureFlag.BindRenderTarget,
                new Vector3i(16, 16, 1)));
            var rawDepthTexture = new RawTexture(new RawTextureParams(
                TextureType.Texture2D,
                TextureFormat.TexFormatD24UnormS8Uint,
                TextureFlag.BindDepthStencil,
                new Vector3i(16, 16, 1)));

            var renderTargetView = RenderTargetView.Texture(rawTexture);
            var readOnlyDepthView = RenderTargetView.ReadOnlyDepth(rawDepthTexture);

            Assert.Equal(TextureFormat.TexFormatRgba8Unorm, renderTargetView.GetFormat());
            Assert.Equal(TextureFormat.TexFormatD24UnormS8Uint, readOnlyDepthView.GetFormat());
        });
    }

    [Fact]
    public void Raw_texture_creates_native_handle_when_graphics_is_initialized()
    {
        WithInitializedGraphics(() =>
        {
            var rawTexture = new RawTexture(new RawTextureParams(
                TextureType.Texture2D,
                TextureFormat.TexFormatRgba8Unorm,
                TextureFlag.BindRenderTarget,
                new Vector3i(32, 16, 1)));

            var handle = rawTexture.GetHandles().Texture;

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
                TextureFormat.TexFormatD32Float,
                TextureFlag.BindDepthStencil,
                new Vector3i(32, 16, 1)));

            var writableView = Assert.IsAssignableFrom<ITextureView>(rawTexture.GetHandles().DepthStencilView);
            var readOnlyView = Assert.IsAssignableFrom<ITextureView>(rawTexture.GetHandles().ReadOnlyDepthStencilView);

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
                TextureFormat.TexFormatRgba8Unorm,
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
                TextureFormat.TexFormatRgba8Unorm,
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
    public void Raw_texture_updates_and_reads_native_texture_data()
    {
        WithInitializedGraphics(() =>
        {
            var rawTexture = new RawTexture(new RawTextureParams(
                TextureType.Texture2D, TextureFormat.TexFormatR8Unorm, TextureFlag.None, new Vector3i(4, 3, 1), NumLevels: 1));
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
                TextureType.Texture2D, TextureFormat.TexFormatRgba8Unorm, TextureFlag.None, new Vector3i(4, 4, 1), NumLevels: 1));
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
                TextureType.Texture2D, TextureFormat.TexFormatRgba8Unorm, TextureFlag.BindRenderTarget,
                new Vector3i(8, 4, 1), NumLevels: 1));
            var wrapped = new TestRawTexture();
            Assert.True(wrapped.CreateForTest(new RawTextureParams(
                TextureType.Texture2D, TextureFormat.TexFormatRgba8Unorm, TextureFlag.BindUnorderedAccess,
                new Vector3i(1, 1, 1), NumLevels: 1)));

            Assert.True(wrapped.CreateFromHandle(source.GetHandles().Texture!, TextureFormat.TexFormatUnknown, 1));
            Assert.Same(source.GetHandles().Texture, wrapped.GetHandles().Texture);
            Assert.Equal(new Vector3i(8, 4, 1), wrapped.GetParams().Size);
            Assert.Equal(TextureFlag.BindRenderTarget | TextureFlag.BindUnorderedAccess |
                TextureFlag.NoMultiSampledAutoResolve,
                wrapped.GetParams().Flags);
            Assert.NotNull(wrapped.GetHandles().ShaderResourceView);
            Assert.NotNull(wrapped.GetHandles().RenderTargetView);

            wrapped.Destroy();
            Assert.False(source.GetHandles().Texture!.IsNull);
        });
    }

    [Fact]
    public void Raw_texture_wraps_native_handle_without_shader_resource_binding()
    {
        WithInitializedGraphics(() =>
        {
            using var name = new CString("RawTextureTests external RTV");
            using var desc = new TextureDesc
            {
                Name = name.Pointer,
                Type = ResourceDimension.ResourceDimTex2d,
                Usage = Usage.UsageDefault,
                Format = global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatRgba8Unorm,
                Width = 8,
                Height = 4,
                MipLevels = 1,
                SampleCount = 1,
                ArraySize = 1,
                BindFlags = BindFlags.BindRenderTarget
            };
            _fixture.RenderDevice.GetRenderDevice()!.CreateTexture(desc, null!, out var nativeTexture);
            Assert.NotNull(nativeTexture);

            var wrapped = new TestRawTexture();
            try
            {
                Assert.True(wrapped.CreateFromHandle(nativeTexture!, TextureFormat.TexFormatUnknown, 1));
                Assert.Null(wrapped.GetHandles().ShaderResourceView);
                Assert.NotNull(wrapped.GetHandles().RenderTargetView);
            }
            finally
            {
                wrapped.Destroy();
                _ = nativeTexture!.Release();
            }
        });
    }

    [Fact]
    public void Raw_texture_external_imports_assert_on_unsupported_backend()
    {
        var texture = new TestRawTexture();

#if DEBUG
        Assert.Throws<InvalidOperationException>(() =>
            texture.CreateFromD3D11Texture2D(0, TextureFormat.TexFormatRgba8Unorm, 1));
        Assert.Throws<InvalidOperationException>(() =>
            texture.CreateFromD3D12Resource(0, TextureFormat.TexFormatRgba8Unorm, 1));
        Assert.Throws<InvalidOperationException>(() =>
            texture.CreateFromVulkanImage(0, new RawTextureParams(
                TextureType.Texture2D, TextureFormat.TexFormatRgba8Unorm, TextureFlag.None, Vector3i.One)));
#else
        Assert.False(texture.CreateFromD3D11Texture2D(0, TextureFormat.TexFormatRgba8Unorm, 1));
        Assert.False(texture.CreateFromD3D12Resource(0, TextureFormat.TexFormatRgba8Unorm, 1));
        Assert.False(texture.CreateFromVulkanImage(0, new RawTextureParams(
            TextureType.Texture2D, TextureFormat.TexFormatRgba8Unorm, TextureFlag.None, Vector3i.One)));
#endif
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
        _fixture.InitializeGraphics(parameters);
        try
        {
            action();
        }
        finally
        {
            _fixture.ResetGraphics();
            RbfxTestData.RemoveResourceDirs(_fixture.Resources);
        }
    }
}
