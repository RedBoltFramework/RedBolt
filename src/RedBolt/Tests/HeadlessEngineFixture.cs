namespace RedBolt.Tests;

using RedBolt;
using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Input;
using RedBolt.RenderAPI;
using RedBolt.Resource;

/// <summary>
/// Fixture that boots a headless RedBolt engine once per test collection.
/// Tests that need an initialized Engine should use [Collection(nameof(HeadlessEngineCollection))].
/// </summary>
public class HeadlessEngineFixture : IDisposable
{
    public HeadlessEngineFixture()
    {
        Engine = new EngineSystem();
        var parameters = new EngineParameters
        {
            ApplicationName = "RedBolt.Tests",
            Headless = true,
            ResourcePaths = "CoreData;Data",
            ResourcePrefixPaths = ".;..",
            WindowMaximize = false,
            WindowWidth = 1280,
            WindowHeight = 720
        };
        Engine.Initialize(parameters);

        var model = new Model { Name = "Models/ProceduralBox" };
        model.SetNumGeometries(1);
        var geometry = new Geometry();
        geometry.SetDrawRange(PrimitiveType.TriangleList, 0, 36, 0, 36);
        model.SetGeometry(0, 0, geometry);
        Resources.AddManualResource(model);
    }

    public EngineSystem Engine { get; }

    public Context Context => Engine.Context;

    public Time Time => Engine.GetSubsystem<Time>()!;

    public ResourceCache Resources => Engine.GetSubsystem<ResourceCache>()!;

    public InputSystem Input => Engine.GetSubsystem<InputSystem>()!;

    public Renderer Renderer => Engine.GetSubsystem<Renderer>()!;

    public Graphics Graphics => Engine.GetSubsystem<Graphics>()!;

    public RenderDevice RenderDevice => Engine.GetSubsystem<RenderDevice>()!;

    public RenderContext RenderContext => RenderDevice.GetRenderContext();

    public PipelineStateCache PipelineStateCache => Engine.GetSubsystem<PipelineStateCache>()!;

    public virtual void Dispose()
    {
        Engine.Shutdown();
    }

}

[CollectionDefinition(nameof(HeadlessEngineCollection), DisableParallelization = true)]
public sealed class HeadlessEngineCollection : ICollectionFixture<HeadlessEngineFixture>
{
}

public sealed class GraphicsEngineFixture : HeadlessEngineFixture
{
    private readonly EngineParameters _defaultGraphicsParameters = new()
    {
        ApplicationName = "RedBolt.Tests",
        RenderBackend = RenderBackend.OpenGL,
        FullScreen = false,
        Borderless = false,
        MultiSample = 1,
        WindowMaximize = false,
        WindowWidth = 64,
        WindowHeight = 64
    };

    public GraphicsEngineFixture()
    {
        InitializeGraphics(_defaultGraphicsParameters);
    }

    public void InitializeGraphics(EngineParameters parameters)
    {
        if (Graphics.IsInitialized)
        {
            Graphics.SetMode(parameters.WindowWidth, parameters.WindowHeight,
                parameters.FullScreen, parameters.Borderless, parameters.WindowResizable,
                false, parameters.VSync, false,
                parameters.MultiSample, parameters.Monitor, parameters.RefreshRate);
            return;
        }
        if (Input.IsInitialized)
            Input.Shutdown();

        Input.Initialize(parameters);
        var deviceSettings = new RenderDeviceSettings
        {
            Backend = RenderAPIUtils.SelectRenderBackend(parameters.RenderBackend),
            ExternalWindowHandle = parameters.ExternalWindow,
            GpuDebug = parameters.GPUDebug,
            AdapterId = parameters.RenderAdapterId.HasValue
                ? checked((int)parameters.RenderAdapterId.Value)
                : null
        };
        var windowSettings = new WindowSettings
        {
            Mode = parameters.FullScreen
                ? WindowMode.Fullscreen
                : parameters.Borderless
                    ? WindowMode.Borderless
                    : WindowMode.Windowed,
            Size = new Vector2i(parameters.WindowWidth, parameters.WindowHeight),
            Title = parameters.WindowTitle,
            Resizable = parameters.WindowResizable,
            Monitor = parameters.Monitor,
            VSync = parameters.VSync,
            RefreshRate = parameters.RefreshRate,
            MultiSample = parameters.MultiSample,
            SRgb = true
        };
        Graphics.Initialize(parameters, deviceSettings, windowSettings, Input);
        Graphics.InitializePipelineStateCache(FileIdentifier.FromUri(parameters.PsoCache));
        RenderDevice.RestoreDeviceState();
    }

    public void ResetGraphics()
    {
        if (!Graphics.IsInitialized)
            InitializeGraphics(_defaultGraphicsParameters);
        else
            Graphics.SetMode(_defaultGraphicsParameters.WindowWidth, _defaultGraphicsParameters.WindowHeight,
                _defaultGraphicsParameters.FullScreen, _defaultGraphicsParameters.Borderless,
                _defaultGraphicsParameters.WindowResizable, false, _defaultGraphicsParameters.VSync, false,
                _defaultGraphicsParameters.MultiSample, _defaultGraphicsParameters.Monitor,
                _defaultGraphicsParameters.RefreshRate);
    }

    public override void Dispose()
    {
        if (Graphics.IsInitialized)
        {
            RenderDevice.InvalidateDeviceState();
            Graphics.Close();
        }
        if (Input.IsInitialized)
            Input.Shutdown();
        base.Dispose();
    }
}

[CollectionDefinition(nameof(GraphicsEngineCollection), DisableParallelization = true)]
public sealed class GraphicsEngineCollection : ICollectionFixture<GraphicsEngineFixture>
{
}

[CollectionDefinition(nameof(GraphicsLifecycleCollection), DisableParallelization = true)]
public sealed class GraphicsLifecycleCollection
{
}
