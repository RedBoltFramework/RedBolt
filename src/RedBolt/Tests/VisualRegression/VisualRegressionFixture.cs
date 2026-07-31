namespace RedBolt.Tests;

using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.RenderAPI;
using RedBolt.Resource;
using RedBolt.Scene;

internal readonly record struct VisualRegressionView(
    Scene Scene,
    Camera Camera,
    IntRect Rect,
    Camera? RightEye = null,
    Camera? CullCamera = null);

internal readonly record struct VisualRegressionCapture(
    Scene Scene,
    Camera Camera,
    string ReferenceName,
    int ChannelTolerance = 8,
    double MaxDifferingPixelRatio = 0.02,
    double MaxAverageChannelError = 2.0);

internal readonly record struct VisualRegressionImageCapture(
    VisualRegressionImage Image,
    string ReferenceName,
    int ChannelTolerance = 8,
    double MaxDifferingPixelRatio = 0.02,
    double MaxAverageChannelError = 2.0);

internal sealed class VisualRegressionFixture : IDisposable
{
    private readonly GraphicsEngineFixture _engineFixture;
    private int _width;
    private int _height;

    public VisualRegressionFixture(int width = 128, int height = 96, int multiSample = 1,
        RenderBackend backend = RenderBackend.OpenGL,
        MaterialQuality textureQuality = MaterialQuality.High,
        TextureFilterMode textureFilterMode = TextureFilterMode.Trilinear,
        int textureAnisotropy = 4)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height));

        _width = width;
        _height = height;
        if (multiSample < 1)
            throw new ArgumentOutOfRangeException(nameof(multiSample));

        _engineFixture = new GraphicsEngineFixture();
        RbfxTestData.AddResourceDirs(Resources);

        _engineFixture.InitializeGraphics(new EngineParameters
        {
            ApplicationName = "RedBolt.Tests.VisualRegression",
            RenderBackend = backend,
            FullScreen = false,
            Borderless = false,
            MultiSample = multiSample,
            TextureQuality = textureQuality,
            TextureFilterMode = textureFilterMode,
            TextureAnisotropy = textureAnisotropy,
            VSync = false,
            WindowHidden = true,
            WindowMaximize = false,
            WindowWidth = width,
            WindowHeight = height
        });
    }

    public GraphicsEngineFixture EngineFixture => _engineFixture;

    public ResourceCache Resources => _engineFixture.Resources;

    public Renderer Renderer => _engineFixture.Renderer;

    public Graphics Graphics => _engineFixture.Graphics;

    public int Width => _width;

    public int Height => _height;

    public VisualRegressionImage Capture(Scene scene, Camera camera)
    {
        return Capture(scene, camera, 2);
    }

    public VisualRegressionImage CaptureUnwarmed(Scene scene, Camera camera)
    {
        return Capture(scene, camera, 0);
    }

    public IReadOnlyList<VisualRegressionImage> CaptureFrames(Scene scene, Camera camera,
        int frameCount, int warmupFrames = 2)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(camera);
        if (frameCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(frameCount));
        if (warmupFrames < 0)
            throw new ArgumentOutOfRangeException(nameof(warmupFrames));

        using var viewport = new Viewport(scene, camera, new IntRect(0, 0, _width, _height));
        Renderer.SetNumViewports(1);
        Renderer.SetViewport(0, viewport);
        try
        {
            for (var i = 0; i < warmupFrames; ++i)
                RenderFrame();

            var frames = new List<VisualRegressionImage>(frameCount);
            for (var i = 0; i < frameCount; ++i)
            {
                RenderFrame();
                frames.Add(TakeScreenShot());
            }
            return frames;
        }
        finally
        {
            Renderer.SetNumViewports(0);
        }
    }

    public void Resize(int width, int height)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height));
        Assert.True(Graphics.SetMode(width, height));
        _width = width;
        _height = height;
    }

    public VisualRegressionImage CaptureToRenderSurface(Scene scene, Camera camera)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(camera);

        using var texture = new Texture2D { Name = "VisualRegression.RenderTarget" };
        Assert.True(texture.SetSize(_width, _height, TextureFormat.TexFormatRgba8Unorm,
            TextureFlag.BindRenderTarget));
        var surface = Assert.IsType<RenderSurface>(texture.GetRenderSurface());
        return CaptureToRenderSurface(surface, scene, camera);
    }

    public VisualRegressionImage CaptureToRenderSurface(RenderSurface surface, Scene scene, Camera camera,
        int warmupFrames = 2)
    {
        RenderToSurface(surface, scene, camera, warmupFrames);
        return ReadSurfaceImage(surface);
    }

    public void RenderToSurface(RenderSurface surface, Scene scene, Camera camera, int warmupFrames = 2)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(camera);
        if (warmupFrames < 0)
            throw new ArgumentOutOfRangeException(nameof(warmupFrames));

        using var viewport = new Viewport(scene, camera, new IntRect(0, 0, _width, _height));
        Renderer.SetNumViewports(1);
        Renderer.SetViewport(0, viewport);
        Renderer.SetBackbufferRenderSurface(surface);
        try
        {
            for (var i = 0; i < warmupFrames; ++i)
                RenderFrame();
        }
        finally
        {
            Renderer.SetBackbufferRenderSurface(null);
            Renderer.SetNumViewports(0);
        }
    }

    public void RenderFrameForVisualTest() => RenderFrame();

    public VisualRegressionImage ReadTextureImage(Texture2D texture)
    {
        ArgumentNullException.ThrowIfNull(texture);
        using var image = Assert.IsType<Image>(texture.GetImage());
        return VisualRegressionImage.FromImage(image);
    }

    private VisualRegressionImage Capture(Scene scene, Camera camera, int warmupFrames)
    {
        return Capture(new[]
        {
            new VisualRegressionView(scene, camera, new IntRect(0, 0, _width, _height))
        }, warmupFrames);
    }

    public VisualRegressionImage Capture(params VisualRegressionView[] views)
    {
        return Capture(views, 2);
    }

    private VisualRegressionImage Capture(VisualRegressionView[] views, int warmupFrames)
    {
        ArgumentNullException.ThrowIfNull(views);
        if (views.Length == 0)
            throw new ArgumentException("At least one visual regression view is required.", nameof(views));

        Renderer.SetNumViewports((uint)views.Length);
        var viewports = new Viewport[views.Length];
        try
        {
            for (var i = 0; i < views.Length; ++i)
            {
                var view = views[i];
                viewports[i] = new Viewport(view.Scene, view.Camera, view.Rect);
                if (view.RightEye is not null)
                    viewports[i].SetEye(view.RightEye, 1);
                if (view.CullCamera is not null)
                    viewports[i].SetCullCamera(view.CullCamera);
                Renderer.SetViewport((uint)i, viewports[i]);
            }

            for (var i = 0; i < warmupFrames; ++i)
                RenderFrame();

            return TakeScreenShot();
        }
        finally
        {
            foreach (var viewport in viewports)
                viewport?.Dispose();
            Renderer.SetNumViewports(0);
        }
    }

    public void AssertMatches(Scene scene, Camera camera, string referenceName,
        int channelTolerance = 8, double maxDifferingPixelRatio = 0.02,
        double maxAverageChannelError = 2.0)
    {
        AssertMatches(Capture(scene, camera), referenceName, channelTolerance,
            maxDifferingPixelRatio, maxAverageChannelError);
    }

    public void AssertMatches(string referenceName, params VisualRegressionView[] views)
    {
        AssertMatches(Capture(views), referenceName);
    }

    public void AssertMatches(params VisualRegressionCapture[] captures)
    {
        ArgumentNullException.ThrowIfNull(captures);
        if (captures.Length == 0)
            throw new ArgumentException("At least one visual regression capture is required.", nameof(captures));

        foreach (var capture in captures)
        {
            AssertMatches(capture.Scene, capture.Camera, capture.ReferenceName,
                capture.ChannelTolerance, capture.MaxDifferingPixelRatio,
                capture.MaxAverageChannelError);
        }
    }

    public void AssertMatches(params VisualRegressionImageCapture[] captures)
    {
        ArgumentNullException.ThrowIfNull(captures);
        if (captures.Length == 0)
            throw new ArgumentException("At least one visual regression capture is required.", nameof(captures));

        foreach (var capture in captures)
        {
            AssertMatches(capture.Image, capture.ReferenceName, capture.ChannelTolerance,
                capture.MaxDifferingPixelRatio, capture.MaxAverageChannelError);
        }
    }

    public void AssertMatches(VisualRegressionImage actual, string referenceName,
        int channelTolerance = 8, double maxDifferingPixelRatio = 0.02,
        double maxAverageChannelError = 2.0)
    {
        var referenceFile = VisualRegressionReferences.GetPath(referenceName);
        if (VisualRegressionReferences.UpdateReferences)
        {
            actual.SavePng(referenceFile);
            return;
        }

        var expected = VisualRegressionImage.Load(referenceFile);
        var comparison = actual.Compare(expected, channelTolerance, maxDifferingPixelRatio,
            maxAverageChannelError);
        if (comparison.IsMatch)
            return;

        var shotFile = VisualRegressionReferences.GetShotPath(referenceFile);
        actual.SavePng(shotFile);

        var artifactDirectory = VisualRegressionReferences.GetArtifactDirectory();
        var baseName = Path.GetFileNameWithoutExtension(referenceFile);
        actual.SavePng(Path.Combine(artifactDirectory, $"{baseName}.actual.png"));
        if (actual.Width == expected.Width && actual.Height == expected.Height)
        {
            actual.CreateDifference(expected).SavePng(
                Path.Combine(artifactDirectory, $"{baseName}.diff.png"));
        }

        Assert.True(comparison.IsMatch,
            $"Visual screenshot did not match '{referenceFile}': {comparison}. "
            + $"Shot: {shotFile}. Artifacts: {artifactDirectory}");
    }

    public void Dispose()
    {
        RbfxTestData.RemoveResourceDirs(Resources);
        _engineFixture.Dispose();
    }

    private void RenderFrame()
    {
        Assert.True(Graphics.BeginFrame());
        try
        {
            Renderer.Update(0.0f);
            Renderer.Render();
            Graphics.ResetRenderTargets();
        }
        finally
        {
            Graphics.EndFrame();
        }
    }

    private VisualRegressionImage TakeScreenShot()
    {
        using var screenshot = new Image();
        Assert.True(Graphics.TakeScreenShot(screenshot));
        return VisualRegressionImage.FromImage(screenshot);
    }

    private VisualRegressionImage ReadSurfaceImage(RenderSurface surface)
    {
        var texture = Assert.IsAssignableFrom<Texture2D>(surface.GetParentTexture());
        return ReadTextureImage(texture);
    }
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
internal sealed class VisualFactAttribute : Xunit.FactAttribute
{
    public VisualFactAttribute() => Skip = VisualRegressionReferences.SkipReason;
}
