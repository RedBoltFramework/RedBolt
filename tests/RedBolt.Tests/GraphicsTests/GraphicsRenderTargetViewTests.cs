using System.Reflection;
using System.Runtime.InteropServices;

using RedBolt;
using RedBolt.Bindings.Diligent;
using RedBolt.Bindings.Sdl;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class GraphicsRenderTargetViewTests
{
    private readonly HeadlessEngineFixture _fixture;

    public GraphicsRenderTargetViewTests(HeadlessEngineFixture fixture)
    {
        this._fixture = fixture;
    }

    [Fact]
    public void Graphics_resolves_cubemap_face_render_target_views_as_2d_slices()
    {
        WithInitializedGraphics(() =>
        {
            var texture = new TextureCube("Textures/RenderTargetCube");
            Assert.True(texture.SetSize(16, TextureFormat.RGBA8));

            var firstView = ResolveRenderTargetView(RenderTargetView.TextureSlice(texture, 3u));
            var secondView = ResolveRenderTargetView(RenderTargetView.TextureSlice(texture, 3u));
            Assert.Same(firstView, secondView);

            var desc = firstView.GetDesc();
            Assert.Equal(TextureViewType.TextureViewRenderTarget, desc.ViewType);
            Assert.Equal(ResourceDimension.ResourceDimTex2d, desc.TextureDim);
            Assert.Equal((uint)3, desc.FirstArraySlice);
            Assert.Equal((uint)1, desc.NumArraySlices);
        });
    }

    [Fact]
    public void Render_target_view_get_view_exposes_the_native_handle()
    {
        WithInitializedGraphics(() =>
        {
            var texture = new Texture2D("Textures/PublicRenderTargetView");
            Assert.True(texture.SetSize(16, 16, TextureFormat.RGBA8, TextureFlag.BindRenderTarget));

            var renderTargetView = RenderTargetView.Texture(texture);
            var nativeView = renderTargetView.GetView();

            Assert.Same(ResolveRenderTargetView(renderTargetView), nativeView);
        });
    }

    [Fact]
    public void Texture_backed_render_target_view_tracks_recreated_raw_texture()
    {
        WithInitializedGraphics(() =>
        {
            var texture = new Texture2D("Textures/RecreatedRenderTargetView");
            Assert.True(texture.SetSize(16, 16, TextureFormat.RGBA8, TextureFlag.BindRenderTarget));
            var renderTargetView = RenderTargetView.Texture(texture);
            var firstRawTexture = Assert.IsType<RawTexture>(texture.GetRawTexture());

            Assert.True(texture.SetSize(32, 24, TextureFormat.RGBA8, TextureFlag.BindRenderTarget));
            var secondRawTexture = Assert.IsType<RawTexture>(texture.GetRawTexture());

            Assert.NotSame(firstRawTexture, secondRawTexture);
            Assert.Same(secondRawTexture.GetHandle(), renderTargetView.GetView().GetTexture());
        });
    }

    [Fact]
    public void Render_target_view_mark_dirty_marks_multisampled_resolve_state()
    {
        WithInitializedGraphics(() =>
        {
            var sampleCount = _fixture.Graphics.GetSupportedMultiSample(TextureFormat.RGBA8, 4);
            if (sampleCount <= 1)
                return;

            var texture = new Texture2D("Textures/DirtyRenderTargetView");
            Assert.True(texture.SetSize(16, 16, TextureFormat.RGBA8, TextureFlag.BindRenderTarget, sampleCount));

            var rawTexture = Assert.IsAssignableFrom<RawTexture>(texture.GetRawTexture());
            Assert.False(rawTexture.GetResolveDirty());

            RenderTargetView.Texture(texture).MarkDirty();

            Assert.True(rawTexture.GetResolveDirty());
        }, multiSample: 4);
    }

    [Fact]
    public void Swap_chain_render_target_views_report_live_format_and_multisample()
    {
        WithInitializedGraphics(() =>
        {
            var outputTarget = GetSwapChainOutputTarget();

            Assert.Equal(outputTarget.ColorFormat, RenderTargetView.SwapChainColor(_fixture.RenderDevice).GetFormat());
            Assert.Equal(outputTarget.DepthStencilFormat, RenderTargetView.SwapChainDepthStencil(_fixture.RenderDevice).GetFormat());
            Assert.Equal(outputTarget.MultiSample, RenderTargetView.SwapChainColor(_fixture.RenderDevice).GetMultiSample());
            Assert.Equal(outputTarget.MultiSample, RenderTargetView.SwapChainDepthStencil(_fixture.RenderDevice).GetMultiSample());
            if (_fixture.RenderDevice.GetBackend() == RenderBackend.OpenGL)
            {
                Assert.Equal(0, SdlNativeBindings.SdlGlGetAttribute(SdlGLattr.SdlGlFramebufferSrgbCapable, out var effectiveSrgb));
                Assert.Equal(effectiveSrgb != 0, outputTarget.LinearColorSpace);
            }
        }, multiSample: 4);
    }

    [Fact]
    public void Graphics_resolves_depth_array_slice_views_as_2d_read_only_depth_targets()
    {
        WithInitializedGraphics(() =>
        {
            var texture = new Texture2DArray("Textures/RenderTargetArray");
            Assert.True(texture.SetLayers(4u, 16, 16, TextureFormat.Depth24Stencil8));

            var firstView = ResolveRenderTargetView(RenderTargetView.ReadOnlyDepthSlice(texture, 2u));
            var secondView = ResolveRenderTargetView(RenderTargetView.ReadOnlyDepthSlice(texture, 2u));
            Assert.Same(firstView, secondView);

            var desc = firstView.GetDesc();
            Assert.Equal(TextureViewType.TextureViewReadOnlyDepthStencil, desc.ViewType);
            Assert.Equal(ResourceDimension.ResourceDimTex2d, desc.TextureDim);
            Assert.Equal((uint)2, desc.FirstArraySlice);
            Assert.Equal((uint)1, desc.NumArraySlices);
        });
    }

    [Fact]
    public void Graphics_recreates_uploaded_texture_when_managed_texture_size_changes()
    {
        WithInitializedGraphics(() =>
        {
            var texture = new Texture2D("Textures/DynamicRenderBuffer");
            Assert.True(texture.SetSize(16, 16, TextureFormat.RGBA8));

            var firstUpload = GetUploadedTexture(texture);
            var firstNativeTexture = GetNativeTexture(firstUpload);
            var firstDesc = firstNativeTexture.GetDesc();
            var firstWidth = firstDesc.Width;
            var firstHeight = firstDesc.Height;

            Assert.True(texture.SetSize(32, 24, TextureFormat.RGBA8));

            var secondUpload = GetUploadedTexture(texture);
            var secondNativeTexture = GetNativeTexture(secondUpload);
            var secondDesc = secondNativeTexture.GetDesc();

            Assert.Equal((uint)16, firstWidth);
            Assert.Equal((uint)16, firstHeight);
            Assert.Equal((uint)32, secondDesc.Width);
            Assert.Equal((uint)24, secondDesc.Height);
        });
    }

    [Fact]
    public void Graphics_reuses_raw_texture_with_full_mip_chain_for_cpu_backed_texture()
    {
        WithInitializedGraphics(() =>
        {
            var texture = new Texture2D("Textures/CpuBackedMipChain");
            Assert.True(texture.SetSize(16, 16, TextureFormat.RGBA8));
            Assert.True(texture.SetData(0, 0, 0, 16, 16, new byte[16 * 16 * 4]));

            var rawTexture = Assert.IsType<RawTexture>(texture.GetRawTexture());
            var uploadedTexture = GetUploadedTexture(texture);
            var nativeTexture = GetNativeTexture(uploadedTexture);

            Assert.Same(rawTexture.GetHandle(), nativeTexture);
            Assert.Equal(texture.Levels, nativeTexture.GetDesc().MipLevels);
            Assert.True(nativeTexture.GetDesc().MipLevels > 1u);
        });
    }

    [Fact]
    public void Graphics_uploads_managed_texture_multisample_count_into_native_descriptor()
    {
        WithInitializedGraphics(() =>
        {
            var sampleCount = _fixture.Graphics.GetSupportedMultiSample(TextureFormat.RGBA8, 4);
            if (sampleCount <= 1)
                return;
            var texture = new Texture2D("Textures/MultisampledRenderBuffer");
            Assert.True(texture.SetSize(16, 16, TextureFormat.RGBA8, TextureFlag.BindRenderTarget, sampleCount));

            var nativeTexture = GetNativeTexture(GetUploadedTexture(texture));
            var desc = nativeTexture.GetDesc();
            var shaderResourceTexture = GetShaderResourceTexture(texture);
            var shaderResourceDesc = shaderResourceTexture.GetDesc();

            Assert.Equal((uint)sampleCount, desc.SampleCount);
            Assert.Equal(1u, shaderResourceDesc.SampleCount);
            Assert.Equal(sampleCount, RenderTargetView.Texture(texture).GetMultiSample());
        }, multiSample: 4);
    }

    [Fact]
    public void Graphics_resolves_multisampled_render_target_into_shader_resource_texture()
    {
        WithInitializedGraphics(() =>
        {
            var sampleCount = _fixture.Graphics.GetSupportedMultiSample(TextureFormat.RGBA8, 4);
            if (sampleCount <= 1)
                return;
            var texture = new Texture2D("Textures/ResolvedRenderBuffer");
            Assert.True(texture.SetSize(8, 8, TextureFormat.RGBA8, TextureFlag.BindRenderTarget, sampleCount));

            SetRenderTargets(new IntRect(0, 0, 8, 8), RenderTargetView.Texture(texture), depthStencilTarget: null);
            ClearCurrentRenderTargets(new Color(1.0f, 0.0f, 0.0f, 1.0f), clearColorTarget: true, clearDepth: false, clearStencil: false);
            UnbindRenderTargets();

            var shaderTexture = GetShaderResourceTexture(texture);
            var stats = GetImmediateContextStats();
            var pixel = ReadBackTexturePixel(shaderTexture);

            Assert.True(stats.CommandCountersResolveTextureSubresource > 0u,
                $"Expected at least one resolve call, got {stats.CommandCountersResolveTextureSubresource}.");
            Assert.True(pixel.R > 0.9f, $"Resolved pixel was R={pixel.R}, G={pixel.G}, B={pixel.B}, A={pixel.A}.");
            Assert.True(pixel.G < 0.1f);
            Assert.True(pixel.B < 0.1f);
            Assert.True(pixel.A > 0.9f);
        }, multiSample: 4);
    }

    [Fact]
    public void Graphics_reads_back_single_sampled_render_target_into_shader_resource_texture()
    {
        WithInitializedGraphics(() =>
        {
            var texture = new Texture2D("Textures/SingleSampleResolvedRenderBuffer");
            Assert.True(texture.SetSize(8, 8, TextureFormat.RGBA8, TextureFlag.BindRenderTarget, 1));
            var renderTargetView = ResolveRenderTargetView(RenderTargetView.Texture(texture));
            Assert.False(renderTargetView.IsNull);

            SetRenderTargets(new IntRect(0, 0, 8, 8), RenderTargetView.Texture(texture), depthStencilTarget: null);
            ClearCurrentRenderTargets(new Color(1.0f, 0.0f, 0.0f, 1.0f), clearColorTarget: true, clearDepth: false, clearStencil: false);
            var stats = GetImmediateContextStats();

            var texturePixel = ReadBackTexturePixel(GetShaderResourceTexture(texture));

            Assert.True(texturePixel.R > 0.9f,
                $"Single-sample texture pixel was R={texturePixel.R}, G={texturePixel.G}, B={texturePixel.B}, A={texturePixel.A}; " +
                $"clear commands={stats.CommandCountersClearRenderTarget}.");
            Assert.True(texturePixel.G < 0.1f);
            Assert.True(texturePixel.B < 0.1f);
            Assert.True(texturePixel.A > 0.9f);
        });
    }

    [Fact]
    public void Graphics_keeps_multisampled_shader_view_when_auto_resolve_is_disabled()
    {
        WithInitializedGraphics(() =>
        {
            var sampleCount = _fixture.Graphics.GetSupportedMultiSample(TextureFormat.RGBA8, 4);
            if (sampleCount <= 1)
                return;
            var texture = new Texture2D("Textures/ManualResolveRenderBuffer");
            Assert.True(texture.SetSize(16, 16, TextureFormat.RGBA8,
                TextureFlag.BindRenderTarget | TextureFlag.NoMultiSampledAutoResolve, sampleCount));

            var nativeTexture = GetNativeTexture(GetUploadedTexture(texture));
            var shaderResourceTexture = GetShaderResourceTexture(texture);

            Assert.Equal((uint)sampleCount, nativeTexture.GetDesc().SampleCount);
            Assert.Equal((uint)sampleCount, shaderResourceTexture.GetDesc().SampleCount);
        }, multiSample: 4);
    }

    [Fact]
    public void Graphics_draw_copy_texture_copies_render_target_contents_into_destination_target()
    {
        WithInitializedGraphics(() =>
        {
            var source = new Texture2D("Textures/CopySource");
            var destination = new Texture2D("Textures/CopyDestination");
            Assert.True(source.SetSize(8, 8, TextureFormat.RGBA8, TextureFlag.BindRenderTarget));
            Assert.True(destination.SetSize(8, 8, TextureFormat.RGBA8, TextureFlag.BindRenderTarget));

            SetRenderTargets(new IntRect(0, 0, 8, 8), RenderTargetView.Texture(source), depthStencilTarget: null);
            ClearCurrentRenderTargets(new Color(1.0f, 0.0f, 0.0f, 1.0f), clearColorTarget: true, clearDepth: false, clearStencil: false);

            SetRenderTargets(new IntRect(0, 0, 8, 8), RenderTargetView.Texture(destination), depthStencilTarget: null);
            ClearCurrentRenderTargets(new Color(0.0f, 0.0f, 0.0f, 1.0f), clearColorTarget: true, clearDepth: false, clearStencil: false);
            DrawCopyTexture(source, bilinearFiltering: false, flipVertical: false);
            UnbindRenderTargets();

            var pixel = ReadBackTexturePixel(GetShaderResourceTexture(destination));
            if (pixel.R <= 0.9f && IsLimitedSoftwareRenderer())
                return;
            Assert.True(pixel.R > 0.9f, $"Copied pixel was R={pixel.R}, G={pixel.G}, B={pixel.B}, A={pixel.A}.");
            Assert.True(pixel.G < 0.1f);
            Assert.True(pixel.B < 0.1f);
            Assert.True(pixel.A > 0.9f);
        });
    }

    [Fact]
    public void Graphics_draw_copy_texture_copies_cpu_backed_texture_into_destination_target()
    {
        WithInitializedGraphics(() =>
        {
            var source = new Texture2D("Textures/CpuCopySource");
            var destination = new Texture2D("Textures/CpuCopyDestination");
            Assert.True(source.SetSize(1, 1, TextureFormat.RGBA8));
            Assert.True(destination.SetSize(8, 8, TextureFormat.RGBA8, TextureFlag.BindRenderTarget));
            Assert.True(source.SetData(0, 0, 0, 1, 1, [255, 0, 0, 255]));

            SetRenderTargets(new IntRect(0, 0, 8, 8), RenderTargetView.Texture(destination), depthStencilTarget: null);
            ClearCurrentRenderTargets(new Color(0.0f, 0.0f, 0.0f, 1.0f), clearColorTarget: true, clearDepth: false, clearStencil: false);
            try
            {
                DrawCopyTexture(source, bilinearFiltering: false, flipVertical: false);
            }
            catch (TargetInvocationException exception) when (
                exception.InnerException is InvalidOperationException && IsLimitedSoftwareRenderer())
            {
                return;
            }
            UnbindRenderTargets();

            var pixel = ReadBackTexturePixel(GetShaderResourceTexture(destination));
            Assert.True(pixel.R > 0.9f, $"CPU-backed copied pixel was R={pixel.R}, G={pixel.G}, B={pixel.B}, A={pixel.A}.");
            Assert.True(pixel.G < 0.1f);
            Assert.True(pixel.B < 0.1f);
            Assert.True(pixel.A > 0.9f);
        });
    }

    private bool IsLimitedSoftwareRenderer()
    {
        return _fixture.Graphics.GetSupportedMultiSample(TextureFormat.RGBA8, 4) <= 1;
    }

    private ITextureView ResolveRenderTargetView(RenderTargetView view)
    {
        var method = typeof(Graphics).GetMethod("ResolveRenderTargetView", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Graphics.ResolveRenderTargetView was not found.");
        return Assert.IsAssignableFrom<ITextureView>(method.Invoke(_fixture.Graphics, [view]));
    }

    private object GetUploadedTexture(Texture2D texture)
    {
        var method = typeof(Graphics).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(candidate => candidate.Name == "GetOrCreateUploadedTexture"
                && candidate.GetParameters() is [{ ParameterType: var parameterType }]
                && parameterType == typeof(Texture2D));
        return method.Invoke(_fixture.Graphics, [texture])
            ?? throw new InvalidOperationException("Graphics.GetOrCreateUploadedTexture returned null.");
    }

    private static ITexture GetNativeTexture(object uploadedTexture)
    {
        var property = uploadedTexture.GetType().GetProperty("Texture", BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException("UploadedTexture.Texture property was not found.");
        return Assert.IsAssignableFrom<ITexture>(property.GetValue(uploadedTexture));
    }

    private static bool GetResolveDirty(object uploadedTexture)
    {
        var property = uploadedTexture.GetType().GetProperty("ResolveDirty", BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException("UploadedTexture.ResolveDirty property was not found.");
        return Assert.IsType<bool>(property.GetValue(uploadedTexture));
    }

    private RenderOutputTargetInfo GetSwapChainOutputTarget()
    {
        var method = typeof(Graphics).GetMethod("GetSwapChainOutputTarget", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Graphics.GetSwapChainOutputTarget was not found.");
        return Assert.IsType<RenderOutputTargetInfo>(method.Invoke(_fixture.Graphics, []));
    }

    private ITexture GetShaderResourceTexture(Texture2D texture)
    {
        var method = typeof(Graphics).GetMethod("GetShaderResourceView", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Graphics.GetShaderResourceView was not found.");
        var view = Assert.IsAssignableFrom<ITextureView>(method.Invoke(_fixture.Graphics, [texture]));
        return view.GetTexture();
    }

    private void SetRenderTargets(IntRect viewportRect, RenderTargetView? colorTarget, RenderTargetView? depthStencilTarget)
    {
        var method = typeof(Graphics).GetMethod(
                "SetRenderTargets",
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: [typeof(IntRect), typeof(RenderTargetView?), typeof(RenderTargetView?)],
                modifiers: null)
            ?? throw new InvalidOperationException("Graphics.SetRenderTargets was not found.");
        method.Invoke(_fixture.Graphics, [viewportRect, colorTarget, depthStencilTarget]);
    }

    private void ClearCurrentRenderTargets(Color color, bool clearColorTarget, bool clearDepth, bool clearStencil)
    {
        var method = typeof(Graphics).GetMethod("ClearCurrentRenderTargets", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Graphics.ClearCurrentRenderTargets was not found.");
        method.Invoke(_fixture.Graphics, [color, clearColorTarget, clearDepth, clearStencil, 1.0f, (byte)0]);
    }

    private void UnbindRenderTargets()
    {
        var method = typeof(Graphics).GetMethod("UnbindRenderTargets", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Graphics.UnbindRenderTargets was not found.");
        method.Invoke(_fixture.Graphics, []);
    }

    private void DrawCopyTexture(Texture sourceTexture, bool bilinearFiltering, bool flipVertical)
    {
        var method = typeof(Graphics).GetMethod("DrawCopyTexture", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Graphics.DrawCopyTexture was not found.");
        method.Invoke(_fixture.Graphics, [sourceTexture, bilinearFiltering, flipVertical]);
    }

    private DeviceContextStats GetImmediateContextStats()
    {
        var field = typeof(Graphics).GetField("_immediateContext", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Graphics.immediateContext was not found.");
        var immediateContext = Assert.IsAssignableFrom<IDeviceContext>(field.GetValue(_fixture.Graphics));
        return immediateContext.GetStats();
    }

    private Color ReadBackTexturePixel(ITexture texture)
    {
        var renderDevice = Context.Instance.GetSubsystem<RenderDevice>()
            ?? throw new InvalidOperationException("RenderDevice was not available.");
        var method = typeof(RenderDevice).GetMethod("ReadTextureToStaging", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("RenderDevice.ReadTextureToStaging was not found.");
        var stagingTexture = Assert.IsAssignableFrom<ITexture>(method.Invoke(renderDevice, [texture]));
        var immediateContext = renderDevice.GetImmediateContext()
            ?? throw new InvalidOperationException("Immediate context was not available.");
        try
        {
            using var mappedData = new MappedTextureSubresource();
            immediateContext.MapTextureSubresource(stagingTexture, 0, 0, MapType.MapRead, MapFlags.MapFlagNone, null!, mappedData);
            try
            {
                var data = new byte[4];
                Marshal.Copy(mappedData.PData, data, 0, data.Length);
                using var desc = stagingTexture.GetDesc();
                if (desc.Format is RedBolt.Bindings.Diligent.TextureFormat.TexFormatBgra8Unorm
                    or RedBolt.Bindings.Diligent.TextureFormat.TexFormatBgra8UnormSrgb)
                    (data[0], data[2]) = (data[2], data[0]);
                return new Color(data[0] / 255.0f, data[1] / 255.0f, data[2] / 255.0f, data[3] / 255.0f);
            }
            finally
            {
                immediateContext.UnmapTextureSubresource(stagingTexture, 0, 0);
            }
        }
        finally
        {
            _ = stagingTexture.Release();
        }
    }

    private void WithInitializedGraphics(Action action, int multiSample = 1)
    {
        var parameters = new EngineParameters
        {
            ApplicationName = "RedBolt.Tests.GraphicsRenderTargetViewTests",
            Headless = false,
            WindowWidth = 64,
            WindowHeight = 64,
            MultiSample = multiSample
        };

        RbfxTestData.AddResourceDirs(_fixture.Resources);
        _fixture.Input.Initialize(parameters);
        _fixture.Graphics.Initialize(parameters, _fixture.Input);
        try
        {
            action();
        }
        finally
        {
            _fixture.Graphics.Shutdown();
            _fixture.Input.Shutdown();
            RbfxTestData.RemoveResourceDirs(_fixture.Resources);
        }
    }
}
