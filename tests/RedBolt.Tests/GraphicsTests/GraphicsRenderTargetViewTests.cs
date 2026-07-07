using System.Reflection;
using System.Runtime.InteropServices;

using RedBolt;
using RedBolt.Bindings.Diligent;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class GraphicsRenderTargetViewTests
{
    private readonly HeadlessEngineFixture fixture;

    public GraphicsRenderTargetViewTests(HeadlessEngineFixture fixture)
    {
        this.fixture = fixture;
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
            Assert.Equal((uint)3, desc.FirstSlice);
            Assert.Equal((uint)1, desc.NumSlices);
        });
    }

    [Fact]
    public void Render_target_view_get_view_exposes_the_native_handle()
    {
        WithInitializedGraphics(() =>
        {
            var texture = new Texture2D("Textures/PublicRenderTargetView");
            Assert.True(texture.SetSize(16, 16, TextureFormat.RGBA8));

            var renderTargetView = RenderTargetView.Texture(texture);
            var nativeView = renderTargetView.GetView();

            Assert.Same(ResolveRenderTargetView(renderTargetView), nativeView);
        });
    }

    [Fact]
    public void Render_target_view_mark_dirty_marks_multisampled_resolve_state()
    {
        WithInitializedGraphics(() =>
        {
            var sampleCount = fixture.Graphics.GetSupportedMultiSample(TextureFormat.RGBA8, 4);
            Assert.True(sampleCount > 1, $"Expected multisample level > 1, got {sampleCount}.");

            var texture = new Texture2D("Textures/DirtyRenderTargetView");
            Assert.True(texture.SetSize(16, 16, TextureFormat.RGBA8, sampleCount));

            var uploadedTexture = GetUploadedTexture(texture);
            Assert.False(GetResolveDirty(uploadedTexture));

            RenderTargetView.Texture(texture).MarkDirty();

            Assert.True(GetResolveDirty(uploadedTexture));
        }, multiSample: 4);
    }

    [Fact]
    public void Swap_chain_render_target_views_report_live_format_and_multisample()
    {
        WithInitializedGraphics(() =>
        {
            var outputTarget = GetSwapChainOutputTarget();

            Assert.Equal(outputTarget.ColorFormat, RenderTargetView.SwapChainColor().GetFormat());
            Assert.Equal(outputTarget.DepthStencilFormat, RenderTargetView.SwapChainDepthStencil().GetFormat());
            Assert.Equal(outputTarget.MultiSample, RenderTargetView.SwapChainColor().GetMultiSample());
            Assert.Equal(outputTarget.MultiSample, RenderTargetView.SwapChainDepthStencil().GetMultiSample());
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
            Assert.Equal((uint)2, desc.FirstSlice);
            Assert.Equal((uint)1, desc.NumSlices);
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
    public void Graphics_uploads_managed_texture_multisample_count_into_native_descriptor()
    {
        WithInitializedGraphics(() =>
        {
            var sampleCount = fixture.Graphics.GetSupportedMultiSample(TextureFormat.RGBA8, 4);
            Assert.True(sampleCount > 1, $"Expected multisample level > 1, got {sampleCount}.");
            var texture = new Texture2D("Textures/MultisampledRenderBuffer");
            Assert.True(texture.SetSize(16, 16, TextureFormat.RGBA8, sampleCount));

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
            var sampleCount = fixture.Graphics.GetSupportedMultiSample(TextureFormat.RGBA8, 4);
            Assert.True(sampleCount > 1, $"Expected multisample level > 1, got {sampleCount}.");
            var texture = new Texture2D("Textures/ResolvedRenderBuffer");
            Assert.True(texture.SetSize(8, 8, TextureFormat.RGBA8, sampleCount));

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
            Assert.True(texture.SetSize(8, 8, TextureFormat.RGBA8, 1));
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
            var sampleCount = fixture.Graphics.GetSupportedMultiSample(TextureFormat.RGBA8, 4);
            Assert.True(sampleCount > 1, $"Expected multisample level > 1, got {sampleCount}.");
            var texture = new Texture2D("Textures/ManualResolveRenderBuffer");
            Assert.True(texture.SetSize(16, 16, TextureFormat.RGBA8, sampleCount, noMultiSampledAutoResolve: true));

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
            Assert.True(source.SetSize(8, 8, TextureFormat.RGBA8));
            Assert.True(destination.SetSize(8, 8, TextureFormat.RGBA8));

            SetRenderTargets(new IntRect(0, 0, 8, 8), RenderTargetView.Texture(source), depthStencilTarget: null);
            ClearCurrentRenderTargets(new Color(1.0f, 0.0f, 0.0f, 1.0f), clearColorTarget: true, clearDepth: false, clearStencil: false);

            SetRenderTargets(new IntRect(0, 0, 8, 8), RenderTargetView.Texture(destination), depthStencilTarget: null);
            ClearCurrentRenderTargets(new Color(0.0f, 0.0f, 0.0f, 1.0f), clearColorTarget: true, clearDepth: false, clearStencil: false);
            DrawCopyTexture(source, bilinearFiltering: false, flipVertical: false);
            UnbindRenderTargets();

            var pixel = ReadBackTexturePixel(GetShaderResourceTexture(destination));
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
            Assert.True(destination.SetSize(8, 8, TextureFormat.RGBA8));
            Assert.True(source.SetData(0, 0, 0, 1, 1, [255, 0, 0, 255]));

            SetRenderTargets(new IntRect(0, 0, 8, 8), RenderTargetView.Texture(destination), depthStencilTarget: null);
            ClearCurrentRenderTargets(new Color(0.0f, 0.0f, 0.0f, 1.0f), clearColorTarget: true, clearDepth: false, clearStencil: false);
            DrawCopyTexture(source, bilinearFiltering: false, flipVertical: false);
            UnbindRenderTargets();

            var pixel = ReadBackTexturePixel(GetShaderResourceTexture(destination));
            Assert.True(pixel.R > 0.9f, $"CPU-backed copied pixel was R={pixel.R}, G={pixel.G}, B={pixel.B}, A={pixel.A}.");
            Assert.True(pixel.G < 0.1f);
            Assert.True(pixel.B < 0.1f);
            Assert.True(pixel.A > 0.9f);
        });
    }

    private ITextureView ResolveRenderTargetView(RenderTargetView view)
    {
        var method = typeof(Graphics).GetMethod("ResolveRenderTargetView", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Graphics.ResolveRenderTargetView was not found.");
        return Assert.IsAssignableFrom<ITextureView>(method.Invoke(fixture.Graphics, [view]));
    }

    private object GetUploadedTexture(Texture2D texture)
    {
        var method = typeof(Graphics).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(candidate => candidate.Name == "GetOrCreateUploadedTexture"
                && candidate.GetParameters() is [{ ParameterType: var parameterType }]
                && parameterType == typeof(Texture2D));
        return method.Invoke(fixture.Graphics, [texture])
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
        return Assert.IsType<RenderOutputTargetInfo>(method.Invoke(fixture.Graphics, []));
    }

    private ITexture GetShaderResourceTexture(Texture2D texture)
    {
        var method = typeof(Graphics).GetMethod("GetShaderResourceView", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Graphics.GetShaderResourceView was not found.");
        var view = Assert.IsAssignableFrom<ITextureView>(method.Invoke(fixture.Graphics, [texture]));
        return view.GetTexture();
    }

    private void SetRenderTargets(IntRect viewportRect, RenderTargetView? colorTarget, RenderTargetView? depthStencilTarget)
    {
        var method = typeof(Graphics).GetMethod("SetRenderTargets", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Graphics.SetRenderTargets was not found.");
        method.Invoke(fixture.Graphics, [viewportRect, colorTarget, depthStencilTarget]);
    }

    private void ClearCurrentRenderTargets(Color color, bool clearColorTarget, bool clearDepth, bool clearStencil)
    {
        var method = typeof(Graphics).GetMethod("ClearCurrentRenderTargets", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Graphics.ClearCurrentRenderTargets was not found.");
        method.Invoke(fixture.Graphics, [color, clearColorTarget, clearDepth, clearStencil, 1.0f, (byte)0]);
    }

    private void UnbindRenderTargets()
    {
        var method = typeof(Graphics).GetMethod("UnbindRenderTargets", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Graphics.UnbindRenderTargets was not found.");
        method.Invoke(fixture.Graphics, []);
    }

    private void DrawCopyTexture(Texture sourceTexture, bool bilinearFiltering, bool flipVertical)
    {
        var method = typeof(Graphics).GetMethod("DrawCopyTexture", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Graphics.DrawCopyTexture was not found.");
        method.Invoke(fixture.Graphics, [sourceTexture, bilinearFiltering, flipVertical]);
    }

    private DeviceContextStats GetImmediateContextStats()
    {
        var field = typeof(Graphics).GetField("immediateContext", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Graphics.immediateContext was not found.");
        var immediateContext = Assert.IsAssignableFrom<IDeviceContext>(field.GetValue(fixture.Graphics));
        return immediateContext.GetStats();
    }

    private Color ReadBackTexturePixel(ITexture texture)
    {
        var method = typeof(Graphics).GetMethod("SaveTexture", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Graphics.SaveTexture was not found.");
        var path = Path.Combine(Path.GetTempPath(), $"redbolt-graphics-readback-{Guid.NewGuid():N}.bmp");
        try
        {
            method.Invoke(fixture.Graphics, [texture, path]);

            using var stream = File.OpenRead(path);
            var image = new Image("Textures/Readback");
            Assert.True(image.BeginLoad(stream));
            var level = image.GetDecompressedImageLevel(0u)
                ?? throw new InvalidOperationException("Image level 0 was not available.");
            var data = level.GetData();
            return new Color(data[0] / 255.0f, data[1] / 255.0f, data[2] / 255.0f, data[3] / 255.0f);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
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

        RbfxTestData.AddResourceDirs(fixture.Resources);
        fixture.Input.Initialize(parameters);
        fixture.Graphics.Initialize(parameters, fixture.Input);
        try
        {
            action();
        }
        finally
        {
            fixture.Graphics.Shutdown();
            fixture.Input.Shutdown();
            RbfxTestData.RemoveResourceDirs(fixture.Resources);
        }
    }
}
