using Microsoft.Extensions.Logging;

using RedBolt.Core;
using RedBolt.Input;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.RenderAPI;
using RedBolt.RenderPipeline;
using RedBolt.Resource;
using RedBolt.Scene;
namespace RedBolt.Graphics;
using Math = global::System.Math;

using Scene = global::RedBolt.Scene.Scene;

/// <summary>Light vertex shader variations.</summary>
public enum LightVsVariation
{
    Directional,
    Spot,
    Point,
    Shadow,
    SpotShadow,
    PointShadow,
    ShadowNormalOffset,
    SpotShadowNormalOffset,
    PointShadowNormalOffset,
    MaxLightVsVariations
}

/// <summary>Per-vertex light vertex shader variations.</summary>
public enum VertexLightVsVariation
{
    NoLights,
    OneLight,
    TwoLights,
    ThreeLights,
    FourLights,
    MaxVertexLightVsVariations
}

/// <summary>Light pixel shader variations.</summary>
public enum LightPsVariation
{
    None,
    Spot,
    Point,
    PointMask,
    Specular,
    SpotSpecular,
    PointSpecular,
    PointMaskSpecular,
    Shadow,
    SpotShadow,
    PointShadow,
    PointMaskShadow,
    ShadowSpecular,
    SpotShadowSpecular,
    PointShadowSpecular,
    PointMaskShadowSpecular,
    MaxLightPsVariations
}

/// <summary>Deferred light volume vertex shader variations.</summary>
public enum DeferredLightVsVariation
{
    None,
    Directional,
    Ortho,
    OrthoDirectional,
    MaxDeferredLightVsVariations
}

/// <summary>Deferred light volume pixel shader variations.</summary>
public enum DeferredLightPsVariation
{
    None,
    Spot,
    Point,
    PointMask,
    Specular,
    SpotSpecular,
    PointSpecular,
    PointMaskSpecular,
    Shadow,
    SpotShadow,
    PointShadow,
    PointMaskShadow,
    ShadowSpecular,
    SpotShadowSpecular,
    PointShadowSpecular,
    PointMaskShadowSpecular,
    ShadowNormalOffset,
    SpotShadowNormalOffset,
    PointShadowNormalOffset,
    PointMaskShadowNormalOffset,
    ShadowSpecularNormalOffset,
    SpotShadowSpecularNormalOffset,
    PointShadowSpecularNormalOffset,
    PointMaskShadowSpecularNormalOffset,
    Ortho,
    OrthoSpot,
    OrthoPoint,
    OrthoPointMask,
    OrthoSpecular,
    OrthoSpotSpecular,
    OrthoPointSpecular,
    OrthoPointMaskSpecular,
    OrthoShadow,
    OrthoSpotShadow,
    OrthoPointShadow,
    OrthoPointMaskShadow,
    OrthoShadowSpecular,
    OrthoSpotShadowSpecular,
    OrthoPointShadowSpecular,
    OrthoPointMaskShadowSpecular,
    OrthoShadowNormalOffset,
    OrthoSpotShadowNormalOffset,
    OrthoPointShadowNormalOffset,
    OrthoPointMaskShadowNormalOffset,
    OrthoShadowSpecularNormalOffset,
    OrthoSpotShadowSpecularNormalOffset,
    OrthoPointShadowSpecularNormalOffset,
    OrthoPointMaskShadowSpecularNormalOffset,
    MaxDeferredLightPsVariations
}

/// <summary>Skinning mode.</summary>
public enum SkinningMode
{
    Auto,
    Hardware,
    Software
}

/// <summary>Statistics collected during the last frame.</summary>
// TODO: Move other metrics here.
public struct FrameStatistics
{
    public uint Animations;
    public uint ChangedAnimations;
}

/// <summary>
/// High-level rendering subsystem. Manages drawing of 3D views.
/// </summary>
public sealed class Renderer : RedObject
{
    public const int ShadowMinPixels = 64;
    public const int InstancingBufferDefaultSize = 1024;

    private GraphicsSystem? _graphics;
    private RenderSurface? _backbufferSurface;
    private bool _backbufferSurfaceViewportsDirty;
    private Technique? _defaultTechnique;
    private readonly Zone _defaultZone = new();
    private Geometry? _dirLightGeometry;
    private Geometry? _spotLightGeometry;
    private Geometry? _pointLightGeometry;
    private Material? _defaultMaterial;
    private Texture2D? _defaultLightRamp;
    private Texture2D? _defaultLightSpot;
    private TextureCube? _blackCubeMap;
    private readonly List<Viewport?> _viewports = [];
    private readonly List<(RenderSurface? Surface, Viewport Viewport)> _queuedViewports = [];
    private readonly List<RenderPipelineView> _renderPipelineViews = [];
    private readonly HashSet<Octree> _updatedOctrees = [];
    private readonly HashSet<Technique> _shaderErrorDisplayed = [];
    private FrameInfo _frameInfo;
    private int _textureAnisotropy = 4;
    private TextureFilterMode _textureFilterMode = TextureFilterMode.Trilinear;
    private MaterialQuality _textureQuality = MaterialQuality.High;
    private bool _initialized;
    private bool _resetViews;
    private bool _hardwareSkinningSupported = true;
    private SkinningMode _skinningMode;
    private uint _numSoftwareSkinningBones = 4;
    private FrameStatistics _frameStats;

    public Renderer()
    {
        SubscribeToEvent<GraphicsEvents.ScreenModeEvent>((ref GraphicsEvents.ScreenModeEvent _) => HandleScreenMode());

        // Try to initialize right now, but skip if screen mode is not yet set
        Initialize();
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        _frameStats = default;
        _frameInfo = default;
        _shaderErrorDisplayed.Clear();
        _updatedOctrees.Clear();
        _renderPipelineViews.Clear();
        _queuedViewports.Clear();
        _viewports.Clear();
        _blackCubeMap?.Dispose();
        _blackCubeMap = null;
        _defaultLightSpot = null;
        _defaultLightRamp = null;
        _defaultMaterial?.Dispose();
        _defaultMaterial = null;
        _pointLightGeometry?.Dispose();
        _pointLightGeometry = null;
        _spotLightGeometry?.Dispose();
        _spotLightGeometry = null;
        _dirLightGeometry?.Dispose();
        _dirLightGeometry = null;
        _defaultZone.Dispose();
        _defaultTechnique = null;
        _backbufferSurface = null;
        _graphics = null;
        base.Dispose();
    }

    public ref readonly FrameStatistics GetFrameStats() => ref _frameStats;

    public ref FrameStatistics GetMutableFrameStats() => ref _frameStats;

    public ref readonly FrameInfo GetFrameInfo() => ref _frameInfo;

    public Zone GetDefaultZone() => _defaultZone;

    public Viewport? GetViewport(uint index = 0)
    {
        return index < _viewports.Count ? _viewports[(int)index] : null;
    }

    public void SetViewport(uint index, Viewport? viewport)
    {
        if (index >= _viewports.Count)
            SetNumViewports(index + 1);
        _viewports[(int)index] = viewport;
    }

    public void SetDefaultTechnique(Technique? technique)
    {
        _defaultTechnique = technique;
    }

    public uint GetNumViewports() => (uint)_viewports.Count;

    public uint GetNumViews()
    {
        return (uint)_renderPipelineViews.Count;
    }

    public int GetNumGeometries()
    {
        var count = 0;
        foreach (var renderPipelineView in _renderPipelineViews)
            count += renderPipelineView.GetStats().NumGeometries;
        return count;
    }

    public int GetNumLights()
    {
        var count = 0;
        foreach (var renderPipelineView in _renderPipelineViews)
            count += renderPipelineView.GetStats().NumLights;
        return count;
    }

    public int GetNumShadowMaps()
    {
        var count = 0;
        foreach (var renderPipelineView in _renderPipelineViews)
            count += renderPipelineView.GetStats().NumShadowedLights;
        return count;
    }

    public int GetNumOccluders()
    {
        var count = 0;
        foreach (var renderPipelineView in _renderPipelineViews)
            count += renderPipelineView.GetStats().NumOccluders;
        return count;
    }

    public Viewport? GetViewportForScene(Scene? scene, uint index = 0)
    {
        var matchIndex = 0u;
        foreach (var viewport in _viewports)
        {
            if (viewport is null || !ReferenceEquals(viewport.GetScene(), scene))
                continue;
            if (matchIndex == index)
                return viewport;
            ++matchIndex;
        }

        return null;
    }

    public Technique? GetDefaultTechnique()
    {
        if (_defaultTechnique is null)
            _defaultTechnique = GetSubsystem<ResourceCache>()!.GetResourceOrNull<Technique>("Techniques/NoTexture.xml");

        return _defaultTechnique;
    }

    public Material? GetDefaultMaterial() => _defaultMaterial;

    public Texture2D? GetDefaultLightRamp() => _defaultLightRamp;

    public Texture2D? GetDefaultLightSpot() => _defaultLightSpot;

    public TextureCube? GetBlackCubeMap() => _blackCubeMap;

    public void SetNumViewports(uint count)
    {
        while (_viewports.Count < count)
            _viewports.Add(null);
        while (_viewports.Count > count)
            _viewports.RemoveAt(_viewports.Count - 1);
    }

    public void SetTextureAnisotropy(int level)
    {
        _textureAnisotropy = Math.Max(1, level);
    }

    public int GetTextureAnisotropy() => _textureAnisotropy;

    public void SetTextureFilterMode(TextureFilterMode mode)
    {
        _textureFilterMode = mode;
    }

    public TextureFilterMode GetTextureFilterMode() => _textureFilterMode;

    public void SetTextureQuality(MaterialQuality quality)
    {
        quality = (MaterialQuality)Math.Clamp((uint)quality, (uint)MaterialQuality.Low, (uint)MaterialQuality.High);

        if (quality != _textureQuality)
        {
            _textureQuality = quality;
            ReloadTextures();
        }
    }

    public MaterialQuality GetTextureQuality() => _textureQuality;

    public void SetSkinningMode(SkinningMode mode)
    {
        _skinningMode = mode;
    }

    public SkinningMode GetSkinningMode() => _skinningMode;

    public bool GetUseHardwareSkinning() =>
        _skinningMode == SkinningMode.Auto && _hardwareSkinningSupported || _skinningMode == SkinningMode.Hardware;

    public void SetNumSoftwareSkinningBones(uint numBones)
    {
        _numSoftwareSkinningBones = numBones;
    }

    public uint GetNumSoftwareSkinningBones() => _numSoftwareSkinningBones;

    public Geometry? GetLightGeometry(Light light)
    {
        return light.GetLightType() switch
        {
            LightType.Directional => _dirLightGeometry,
            LightType.Spot => _spotLightGeometry,
            LightType.Point => _pointLightGeometry,
            _ => null
        };
    }

    public Geometry? GetQuadGeometry() => _dirLightGeometry;

    /// <summary>Set backbuffer render surface. Null corresponds to the application backbuffer.</summary>
    public void SetBackbufferRenderSurface(RenderSurface? renderSurface)
    {
        if (!ReferenceEquals(_backbufferSurface, renderSurface))
        {
            _backbufferSurface?.SetNumViewports(0);
            _backbufferSurface = renderSurface;
            _backbufferSurfaceViewportsDirty = true;
        }
    }

    public void QueueRenderSurface(RenderSurface? renderTarget)
    {
        if (renderTarget is null)
            return;

        for (var i = 0; i < renderTarget.GetNumViewports(); ++i)
        {
            if (renderTarget.GetViewport(i) is { } viewport)
                QueueViewport(renderTarget, viewport);
        }
    }

    public void QueueViewport(RenderSurface? renderTarget, Viewport? viewport)
    {
        if (viewport is null)
            return;

        var queuedViewport = (renderTarget, viewport);
        if (!_queuedViewports.Contains(queuedViewport))
            _queuedViewports.Add(queuedViewport);
    }

    public void Update(float timeStep)
    {
        _renderPipelineViews.Clear();

        if (_graphics is null || !_graphics.IsInitialized)
            return;

        _frameInfo.FrameNumber = GetSubsystem<Time>()!.FrameNumber;
        _frameInfo.TimeStep = timeStep;
        _frameInfo.Camera = null;
        _updatedOctrees.Clear();

        if (_backbufferSurfaceViewportsDirty)
        {
            _backbufferSurfaceViewportsDirty = false;
            if (_backbufferSurface is not null)
            {
                var numViewports = _viewports.Count;
                _backbufferSurface.SetNumViewports(numViewports);
                for (var i = 0; i < numViewports; ++i)
                    _backbufferSurface.SetViewport(i, _viewports[i]);
            }
        }

        for (var i = _viewports.Count - 1; i >= 0; --i)
            QueueViewport(_backbufferSurface, _viewports[i]);

        var numMainViewports = _queuedViewports.Count;
        for (var i = 0; i < numMainViewports; ++i)
            UpdateQueuedViewport(i);

        SendEvent<GraphicsEvents.RenderSurfaceUpdateEvent>();

        for (var i = numMainViewports; i < _queuedViewports.Count; ++i)
            UpdateQueuedViewport(i);

        _queuedViewports.Clear();
        _resetViews = false;
    }

    public void Render()
    {
        var renderDevice = GetSubsystem<RenderDevice>()!;

        renderDevice.SetDefaultTextureFilterMode(_textureFilterMode);
        renderDevice.SetDefaultTextureAnisotropy(_textureAnisotropy);

        var hasBackbufferViews = false;
        foreach (var renderPipelineView in _renderPipelineViews)
        {
            if (renderPipelineView.GetFrameInfo().RenderTarget is null)
            {
                hasBackbufferViews = true;
                break;
            }
        }

        if (!hasBackbufferViews)
        {
            _graphics!.ResetRenderTargets();
            _graphics.Clear(ClearTarget.Color | ClearTarget.Depth | ClearTarget.Stencil, _defaultZone.GetFogColor());
        }

        for (var i = _renderPipelineViews.Count - 1; i >= 0; --i)
            _renderPipelineViews[i].Render(_graphics!, _textureQuality);

        foreach (var octree in _updatedOctrees)
            octree.SetLocked(false);

        SendEvent<GraphicsEvents.EndAllViewsRenderEvent>();
    }

    private void UpdateQueuedViewport(int index)
    {
        var (renderTarget, viewport) = _queuedViewports[index];

        if (viewport.GetScene() is null)
            return;

        var renderPipelineView = viewport.GetRenderPipelineView();
        if (renderPipelineView is null || _resetViews)
        {
            viewport.AllocateView();
            renderPipelineView = viewport.GetRenderPipelineView();
        }
        if (renderPipelineView is null || !renderPipelineView.Define(renderTarget, viewport, _graphics!))
            return;

        _renderPipelineViews.Add(renderPipelineView);

        var viewRect = viewport.GetRect();
        var scene = viewport.GetScene();
        if (scene is null)
            return;

        var octree = scene.GetComponent<Octree>()!;
        if (_updatedOctrees.Add(octree))
        {
            _frameInfo.Camera = viewport.GetCamera();
            _frameInfo.ViewSize = viewRect.Size;
            if (_frameInfo.ViewSize == Vector2i.Zero)
                _frameInfo.ViewSize = new Vector2i(_graphics!.Width, _graphics.Height);
            octree.Update(_frameInfo);
            octree.SetLocked(true);

            var debug = scene.GetComponent<DebugRenderer>();
            if (debug is not null && viewport.GetDrawDebug())
                debug.SetView(viewport.GetCamera()!);
        }

        renderPipelineView.Update(_graphics!);
    }

    public void DrawDebugGeometry(bool depthTest)
    {
        foreach (var renderPipelineView in _renderPipelineViews)
        {
            if (!renderPipelineView.GetRenderPipeline().Settings.DrawDebugGeometry)
                continue;

            renderPipelineView.DrawDebugGeometries(depthTest);
            renderPipelineView.DrawDebugLights(depthTest);
        }
    }

    private void Initialize()
    {
        var graphics = GetSubsystem<GraphicsSystem>();
        var renderDevice = GetSubsystem<RenderDevice>();
        var cache = GetSubsystem<ResourceCache>();

        if (graphics is null || !graphics.IsInitialized || cache is null)
            return;

        _hardwareSkinningSupported = true;

        _graphics = graphics;

        _defaultLightRamp = cache.GetResourceOrNull<Texture2D>("Textures/Ramp.png");
        _defaultLightSpot = cache.GetResourceOrNull<Texture2D>("Textures/Spot.png");
        _defaultMaterial = new Material();

        CreateGeometries();

        SetNumViewports(1);

        _initialized = true;

        SubscribeToEvent<InputEvents.InputEndEvent>((ref InputEvents.InputEndEvent _) =>
            UpdateMousePositionsForMainViewports());

        SubscribeToEvent<CoreEvents.RenderUpdateEvent>((ref CoreEvents.RenderUpdateEvent eventData) =>
            Update(eventData.TimeStep));
        SubscribeToEvent<CoreEvents.EndFrameEvent>((ref CoreEvents.EndFrameEvent _) =>
        {
            _frameStats = new FrameStatistics();
        });

        Log.GetLogger().LogInformation("Initialized renderer");
    }

    private void HandleScreenMode()
    {
        if (!_initialized)
            Initialize();
        else
            _resetViews = true;
    }

    private void UpdateMousePositionsForMainViewports()
    {
        var input = GetSubsystem<InputSystem>()!;

        foreach (var viewport in _viewports)
        {
            if (viewport?.GetCamera() is not { } camera)
                continue;

            var rect = viewport.GetEffectiveRect(_backbufferSurface, false);
            var mousePosition = input.MousePosition;
            var rectSizeMinusOne = rect.Size - Vector2i.One;
            var mousePositionNormalized = new Vector2f(
                (float)(mousePosition.X - rect.Left) / rectSizeMinusOne.X,
                (float)(mousePosition.Y - rect.Top) / rectSizeMinusOne.Y);

            camera.SetMousePosition(mousePositionNormalized);
        }
    }

    private void ReloadTextures()
    {
        var cache = GetSubsystem<ResourceCache>();
        if (cache is null)
            return;

        foreach (var texture in cache.GetResources<Texture2D>())
            cache.ReloadResource(texture);

        foreach (var texture in cache.GetResources<TextureCube>())
            cache.ReloadResource(texture);
    }

    private void ReleaseMaterialShaders()
    {
        var cache = GetSubsystem<ResourceCache>();
        if (cache is null)
            return;

        foreach (var material in cache.GetResources<Material>())
            material.ReleaseShaders();
    }

    private void CreateGeometries()
    {
        _dirLightGeometry = CreateGeometry(
            "DirectionalLight",
            [
                -1, 1, 0,
                1, 1, 0,
                1, -1, 0,
                -1, -1, 0
            ],
            [0, 1, 2, 2, 3, 0]);

        _spotLightGeometry = CreateGeometry(
            "SpotLight",
            [
                0.00001f, 0.00001f, 0.00001f,
                0.00001f, -0.00001f, 0.00001f,
                -0.00001f, -0.00001f, 0.00001f,
                -0.00001f, 0.00001f, 0.00001f,
                1.00000f, 1.00000f, 0.99999f,
                1.00000f, -1.00000f, 0.99999f,
                -1.00000f, -1.00000f, 0.99999f,
                -1.00000f, 1.00000f, 0.99999f
            ],
            [
                3, 0, 1, 3, 1, 2, 0, 4, 5, 0, 5, 1,
                3, 7, 4, 3, 4, 0, 7, 3, 2, 7, 2, 6,
                6, 2, 1, 6, 1, 5, 7, 5, 4, 7, 6, 5
            ]);

        _pointLightGeometry = CreateGeometry(
            "PointLight",
            [
                -0.423169f, -1.000000f, 0.423169f, -0.423169f, -1.000000f, -0.423169f,
                0.423169f, -1.000000f, -0.423169f, 0.423169f, -1.000000f, 0.423169f,
                0.423169f, 1.000000f, -0.423169f, -0.423169f, 1.000000f, -0.423169f,
                -0.423169f, 1.000000f, 0.423169f, 0.423169f, 1.000000f, 0.423169f,
                -1.000000f, 0.423169f, -0.423169f, -1.000000f, -0.423169f, -0.423169f,
                -1.000000f, -0.423169f, 0.423169f, -1.000000f, 0.423169f, 0.423169f,
                0.423169f, 0.423169f, -1.000000f, 0.423169f, -0.423169f, -1.000000f,
                -0.423169f, -0.423169f, -1.000000f, -0.423169f, 0.423169f, -1.000000f,
                1.000000f, 0.423169f, 0.423169f, 1.000000f, -0.423169f, 0.423169f,
                1.000000f, -0.423169f, -0.423169f, 1.000000f, 0.423169f, -0.423169f,
                0.423169f, -0.423169f, 1.000000f, 0.423169f, 0.423169f, 1.000000f,
                -0.423169f, 0.423169f, 1.000000f, -0.423169f, -0.423169f, 1.000000f
            ],
            [
                0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7, 8, 9, 10, 8, 10, 11,
                12, 13, 14, 12, 14, 15, 16, 17, 18, 16, 18, 19, 20, 21, 22,
                20, 22, 23, 0, 10, 9, 0, 9, 1, 13, 2, 1, 13, 1, 14, 23, 0, 3,
                23, 3, 20, 17, 3, 2, 17, 2, 18, 21, 7, 6, 21, 6, 22, 7, 16, 19,
                7, 19, 4, 5, 8, 11, 5, 11, 6, 4, 12, 15, 4, 15, 5, 22, 11, 10,
                22, 10, 23, 8, 15, 14, 8, 14, 9, 12, 19, 18, 12, 18, 13, 16, 21,
                20, 16, 20, 17, 0, 23, 10, 1, 9, 14, 2, 13, 18, 3, 17, 20, 6, 11,
                22, 5, 15, 8, 4, 19, 12, 7, 21, 16
            ]);

        _blackCubeMap = new TextureCube();
        _blackCubeMap.SetNumLevels(1);
        _blackCubeMap.SetSize(1, TextureFormat.TexFormatRgba8Unorm);
        _blackCubeMap.SetFilterMode(TextureFilterMode.Nearest);
        var blackCubeMapData = new byte[] { 0, 0, 0, 255 };
        for (var face = CubeMapFace.PositiveX; face < CubeMapFace.MaxCubeMapFaces; ++face)
            _blackCubeMap.SetData(face, 0, 0, 0, 1, 1, blackCubeMapData);
    }

    private static Geometry CreateGeometry(string name, float[] vertexData, uint[] indexData)
    {
        var vertexBuffer = new VertexBuffer();
        vertexBuffer.SetDebugName(name);
        vertexBuffer.SetShadowed(true);
        vertexBuffer.SetSize((uint)(vertexData.Length / 3),
            [new VertexElement(VertexElementType.Vector3, VertexElementSemantic.Position)]);
        vertexBuffer.Update(ToBytes(vertexData));

        var indexBuffer = new IndexBuffer();
        indexBuffer.SetDebugName(name);
        indexBuffer.SetShadowed(true);
        indexBuffer.SetSize((uint)indexData.Length);
        indexBuffer.SetUnpackedData(indexData);

        var geometry = new Geometry();
        geometry.SetVertexBuffer(0, vertexBuffer);
        geometry.SetIndexBuffer(indexBuffer);
        geometry.SetDrawRange(PrimitiveType.TriangleList, 0, (uint)indexData.Length);
        return geometry;
    }

    private static byte[] ToBytes(float[] data)
    {
        var result = new byte[checked(data.Length * sizeof(float))];
        for (var i = 0; i < data.Length; ++i)
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(
                result.AsSpan(i * sizeof(float), sizeof(float)), BitConverter.SingleToInt32Bits(data[i]));
        return result;
    }
}
