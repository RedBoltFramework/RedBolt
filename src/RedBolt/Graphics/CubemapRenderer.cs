using Microsoft.Extensions.Logging;

namespace RedBolt;

public enum CubemapUpdateStage
{
    Idle,
    RenderFaces,
    Ready
}

public struct CubemapUpdateResult
{
    public uint NumRenderedFaces;
    public bool IsComplete;
}

public struct CubemapRenderingSettings : IEquatable<CubemapRenderingSettings>
{
    public const uint DefaultTextureSize = 256;
    public const uint DefaultViewMask = 0xffffffff;
    public const float DefaultNearClip = 0.1f;
    public const float DefaultFarClip = 100.0f;

    public uint TextureSize { get; set; }
    public uint ViewMask { get; set; }
    public float NearClip { get; set; }
    public float FarClip { get; set; }

    public CubemapRenderingSettings()
    {
        TextureSize = DefaultTextureSize;
        ViewMask = DefaultViewMask;
        NearClip = DefaultNearClip;
        FarClip = DefaultFarClip;
    }

    public bool Equals(CubemapRenderingSettings other) =>
        TextureSize == other.TextureSize
        && ViewMask == other.ViewMask
        && NearClip.Equals(other.NearClip)
        && FarClip.Equals(other.FarClip);

    public override bool Equals(object? obj) => obj is CubemapRenderingSettings other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(TextureSize, ViewMask, NearClip, FarClip);

    public static bool operator ==(CubemapRenderingSettings lhs, CubemapRenderingSettings rhs) => lhs.Equals(rhs);

    public static bool operator !=(CubemapRenderingSettings lhs, CubemapRenderingSettings rhs) => !lhs.Equals(rhs);
}

public struct CubemapUpdateParameters : ICloneable
{
    public CubemapRenderingSettings Settings;
    public Vector3f Position;
    public bool SlicedUpdate;
    public bool FilterResult;

    private WeakReference<TextureCube>? _overrideFinalTexture;

    public TextureCube? OverrideFinalTexture
    {
        readonly get => _overrideFinalTexture is not null && _overrideFinalTexture.TryGetTarget(out var texture)
            ? texture
            : null;
        set => _overrideFinalTexture = value is not null ? new WeakReference<TextureCube>(value) : null;
    }

    public CubemapUpdateParameters()
    {
        Settings = new CubemapRenderingSettings();
        Position = default;
        SlicedUpdate = false;
        FilterResult = false;
        OverrideFinalTexture = null;
    }

    public CubemapUpdateParameters(CubemapUpdateParameters rhs)
    {
        Settings = rhs.Settings;
        Position = rhs.Position;
        SlicedUpdate = rhs.SlicedUpdate;
        FilterResult = rhs.FilterResult;
        OverrideFinalTexture = rhs.OverrideFinalTexture;
    }

    public readonly object Clone() => new CubemapUpdateParameters(this);

    public readonly bool IsConsistentWith(in CubemapUpdateParameters rhs) =>
        Settings == rhs.Settings
        && SlicedUpdate == rhs.SlicedUpdate
        && FilterResult == rhs.FilterResult
        && ReferenceEquals(OverrideFinalTexture, rhs.OverrideFinalTexture);
}

/// <summary>Utility class that handles cubemap rendering from scene.</summary>
public class CubemapRenderer : Object
{
    // Needs to match group size in C_FilterCubemap.glsl
    private const int CubeFilterDispatchSize = 16;

    private static readonly Quaternion[] FaceRotations =
    [
        new(0, 90, 0),
        new(0, -90, 0),
        new(-90, 0, 0),
        new(90, 0, 0),
        new(0, 0, 0),
        new(0, 180, 0)
    ];

    private static readonly uint[] RayCounts = [1, 8, 16];
    private static readonly uint[] RayCounts128 = [1, 8, 16, 16, 16, 16, 32, 32];
    private static readonly uint[] RayCounts256 = [1, 8, 16, 16, 16, 16, 16, 32, 32];

    private static ReadOnlySpan<uint> GetRayCounts(int numLevels)
    {
        if (numLevels == 7)
            return RayCounts128;
        if (numLevels == 8)
            return RayCounts256;
        return RayCounts;
    }

    private sealed class CachedPipelineStates
    {
        public int NumLevels;
        public readonly List<PipelineState> PipelineStates = [];
    }

    public event Action<TextureCube>? CubemapRendered;

    private readonly WeakReference<Scene> _scene;
    private readonly Node[] _renderCameraNodes = new Node[(int)CubeMapFace.MaxCubeMapFaces];
    private readonly Viewport[] _viewports = new Viewport[(int)CubeMapFace.MaxCubeMapFaces];
    private readonly RenderPipeline _renderPipeline;
    private readonly TextureCube _viewportTexture = new();
    private readonly TextureCube _filteredTexture = new();

    private CubemapUpdateParameters _currentParams = new();
    private CubemapUpdateStage _updateStage;
    private uint _numFacesToUpdate;
    private uint _numFacesToRender;
    private readonly WeakReference<TextureCube> _currentViewportTexture = new(null!);
    private readonly WeakReference<TextureCube> _currentFilteredTexture = new(null!);
    private bool _viewportsConnectedToSelf;
    private CachedPipelineStates? _cachedPipelineStates;
    private ContextEventSubscription? _endViewRenderSubscription;

    public CubemapRenderer(Scene scene)
    {
        _scene = new WeakReference<Scene>(scene);
        _renderPipeline = new RenderPipeline();
        InitializeRenderPipeline();
        InitializeCameras();
    }

    public override void Dispose()
    {
        for (var face = (int)CubeMapFace.MaxCubeMapFaces - 1; face >= 0; --face)
            _renderCameraNodes[face].Remove();
        CubemapRendered = null;
        _endViewRenderSubscription?.Dispose();
        _endViewRenderSubscription = null;
        base.Dispose();
    }

    private void InitializeRenderPipeline()
    {
        var settings = _renderPipeline.Settings;
        settings.DrawDebugGeometry = false;
        _renderPipeline.SetSettings(settings);
    }

    private void InitializeCameras()
    {
        _scene.TryGetTarget(out var scene);
        System.Diagnostics.Debug.Assert(scene is not null);

        for (var face = 0; face < (int)CubeMapFace.MaxCubeMapFaces; ++face)
        {
            // TODO: port C++ freestanding Node(context); managed nodes currently require a scene owner.
            var cameraNode = scene!.CreateTemporaryChild($"CubemapCamera_{face}");
            cameraNode.SetWorldRotation(FaceRotations[face]);
            var camera = cameraNode.CreateComponent<Camera>();
            camera.SetFov(90.0f);
            camera.SetAspectRatio(1.0f);
            camera.SetDrawDebugGeometry(false);
            _renderCameraNodes[face] = cameraNode;

            _viewports[face] = new Viewport(scene, camera, IntRect.Zero, _renderPipeline);
        }
    }

    private void ConnectViewportsToTexture(TextureCube texture)
    {
        for (var face = 0; face < (int)CubeMapFace.MaxCubeMapFaces; ++face)
        {
            var surface = texture.GetRenderSurface((CubeMapFace)face);
            surface?.SetViewport(0, _viewports[face]);
        }

        _endViewRenderSubscription?.Dispose();
        _endViewRenderSubscription = SubscribeToEvent<GraphicsEvents.EndViewRenderEvent>(texture,
            (ref GraphicsEvents.EndViewRenderEvent _) => ProcessFaceRendered());
    }

    private void DisconnectViewportsFromTexture(TextureCube texture)
    {
        for (var face = 0; face < (int)CubeMapFace.MaxCubeMapFaces; ++face)
        {
            var surface = texture.GetRenderSurface((CubeMapFace)face);
            surface?.SetNumViewports(0);
        }
    }

    public static void DefineTexture(TextureCube texture, CubemapRenderingSettings settings,
        TextureFlag flags = TextureFlag.None)
    {
        texture.SetSize((int)settings.TextureSize, TextureFormat.TexFormatRgba8Unorm, flags | TextureFlag.BindRenderTarget);
        texture.SetFilterMode(TextureFilterMode.Trilinear);
    }

    public CubemapUpdateResult Update(in CubemapUpdateParameters updateParameters)
    {
        if (!_currentParams.IsConsistentWith(updateParameters))
        {
            _currentParams = new CubemapUpdateParameters(updateParameters);

            _updateStage = CubemapUpdateStage.Idle;
            _numFacesToUpdate = 0;
            _numFacesToRender = 0;
        }

        System.Diagnostics.Debug.Assert(_updateStage != CubemapUpdateStage.Ready);

        if (updateParameters.OverrideFinalTexture is not null
            && !IsTextureMatching(updateParameters.OverrideFinalTexture, updateParameters.Settings))
        {
            Log.GetLogger().LogError("Invalid texture is used as override for CubemapRenderer::Update");
            return new CubemapUpdateResult
            {
                NumRenderedFaces = (uint)CubeMapFace.MaxCubeMapFaces,
                IsComplete = true
            };
        }

        if (_updateStage == CubemapUpdateStage.Idle)
            PrepareForUpdate(updateParameters);

        if (_updateStage == CubemapUpdateStage.Idle && !updateParameters.SlicedUpdate)
            return UpdateFull();

        return UpdateSliced();
    }

    private void PrepareForUpdate(in CubemapUpdateParameters updateParameters)
    {
        // Assign current textures
        if (updateParameters.FilterResult)
        {
            _currentViewportTexture.SetTarget(_viewportTexture);
            _currentFilteredTexture.SetTarget(updateParameters.OverrideFinalTexture ?? _filteredTexture);
        }
        else
        {
            _currentViewportTexture.SetTarget(updateParameters.OverrideFinalTexture ?? _viewportTexture);
            _currentFilteredTexture.SetTarget(null!);
        }

        _currentViewportTexture.TryGetTarget(out var currentViewportTexture);
        _currentFilteredTexture.TryGetTarget(out var currentFilteredTexture);

        // Initialize contents
        if (ReferenceEquals(currentViewportTexture, _viewportTexture)
            && !IsTextureMatching(_viewportTexture, updateParameters.Settings))
        {
            DefineTexture(_viewportTexture, updateParameters.Settings, TextureFlag.None);
            ConnectViewportsToTexture(_viewportTexture);
            _viewportsConnectedToSelf = true;
        }
        if (ReferenceEquals(currentFilteredTexture, _filteredTexture)
            && !IsTextureMatching(_filteredTexture, updateParameters.Settings))
        {
            DefineTexture(_filteredTexture, updateParameters.Settings, TextureFlag.BindUnorderedAccess);
        }

        if (!ReferenceEquals(currentViewportTexture, _viewportTexture))
        {
            if (_viewportsConnectedToSelf)
            {
                DisconnectViewportsFromTexture(_viewportTexture);
                _viewportsConnectedToSelf = false;
            }
            if (currentViewportTexture is not null)
                ConnectViewportsToTexture(currentViewportTexture);
        }
        else if (!_viewportsConnectedToSelf)
        {
            ConnectViewportsToTexture(_viewportTexture);
            _viewportsConnectedToSelf = true;
        }

        for (var face = 0; face < (int)CubeMapFace.MaxCubeMapFaces; ++face)
        {
            var camera = _renderCameraNodes[face].GetComponent<Camera>();
            camera!.SetNearClip(updateParameters.Settings.NearClip);
            camera.SetFarClip(updateParameters.Settings.FarClip);
            camera.SetViewMask(updateParameters.Settings.ViewMask);
        }

        foreach (var cameraNode in _renderCameraNodes)
            cameraNode.SetWorldPosition(updateParameters.Position);
    }

    private bool IsTextureMatching(TextureCube? textureCube, CubemapRenderingSettings settings) =>
        textureCube is not null && textureCube.Width == settings.TextureSize;

    private CubemapUpdateResult UpdateFull()
    {
        _updateStage = CubemapUpdateStage.Ready;
        _numFacesToUpdate = 0;
        _numFacesToRender = (uint)CubeMapFace.MaxCubeMapFaces;

        for (var face = 0; face < (int)CubeMapFace.MaxCubeMapFaces; ++face)
            QueueFaceUpdate((CubeMapFace)face);

        return new CubemapUpdateResult
        {
            NumRenderedFaces = (uint)CubeMapFace.MaxCubeMapFaces,
            IsComplete = true
        };
    }

    private CubemapUpdateResult UpdateSliced()
    {
        if (_updateStage == CubemapUpdateStage.Idle)
        {
            _updateStage = CubemapUpdateStage.RenderFaces;
            _numFacesToUpdate = (uint)CubeMapFace.MaxCubeMapFaces;
        }

        if (_updateStage == CubemapUpdateStage.RenderFaces)
        {
            --_numFacesToUpdate;

            var face = (CubeMapFace)_numFacesToUpdate;
            QueueFaceUpdate(face);

            if (_numFacesToUpdate == 0)
            {
                _updateStage = CubemapUpdateStage.Ready;
                _numFacesToRender = 1;
            }
        }

        var isComplete = _updateStage == CubemapUpdateStage.Ready;
        return new CubemapUpdateResult { NumRenderedFaces = 1, IsComplete = isComplete };
    }

    private void ProcessFaceRendered()
    {
        System.Diagnostics.Debug.Assert(_updateStage != CubemapUpdateStage.Ready || _numFacesToRender > 0);

        if (_updateStage == CubemapUpdateStage.Ready && _numFacesToRender > 0)
        {
            --_numFacesToRender;
            if (_numFacesToRender == 0)
            {
                _updateStage = CubemapUpdateStage.Idle;
                ProcessCubemapRendered();
            }
        }
    }

    private void ProcessCubemapRendered()
    {
        _currentViewportTexture.TryGetTarget(out var currentViewportTexture);
        _currentFilteredTexture.TryGetTarget(out var currentFilteredTexture);

        if (!ReferenceEquals(currentViewportTexture, _viewportTexture))
            DisconnectViewportsFromTexture(currentViewportTexture!);

        if (currentFilteredTexture is not null)
            FilterCubemap(currentViewportTexture!, currentFilteredTexture);

        var finalTexture = currentFilteredTexture ?? currentViewportTexture!;
        CubemapRendered?.Invoke(finalTexture);

        _currentViewportTexture.SetTarget(null!);
        _currentFilteredTexture.SetTarget(null!);
    }

    private void QueueFaceUpdate(CubeMapFace face)
    {
        var renderer = GetSubsystem<Renderer>();
        if (renderer is null)
            return;

        _currentViewportTexture.TryGetTarget(out var currentViewportTexture);
        var surface = currentViewportTexture!.GetRenderSurface(face);
        renderer.QueueRenderSurface(surface);
    }

    private void FilterCubemap(TextureCube sourceTexture, TextureCube destTexture)
    {
        var renderDevice = GetSubsystem<RenderDevice>();
        if (!renderDevice!.GetCaps().ComputeShaders)
        {
            Log.GetLogger().LogError("CubemapRenderer::FilterCubemap cannot be executed without compute shader support");
            return;
        }

        var numLevels = destTexture.GetLevels();
        EnsurePipelineStates(numLevels);
        if (_cachedPipelineStates is null)
            return;

        var renderContext = renderDevice.GetRenderContext();
        var drawQueue = renderDevice.GetDefaultQueue();

        using var renderScope = new RenderScope(renderContext, "CubemapRenderer::FilterCubemap");

        var sourceRaw = sourceTexture.GetRawTexture()!;
        var destRaw = destTexture.GetRawTexture()!;

        for (var i = 0; i < numLevels; ++i)
            destRaw.CreateUAV(new RawTextureUAVKey().FromLevel(i));

        drawQueue.Reset();

        for (var i = 0; i < numLevels; ++i)
        {
            var pipelineState = _cachedPipelineStates.PipelineStates[(int)i];
            drawQueue.SetPipelineState(pipelineState);

            drawQueue.AddShaderResource(new StringHash("SourceTexture"), sourceRaw);
            drawQueue.CommitShaderResources();

            drawQueue.AddUnorderedAccessView(new StringHash("OutputTexture"), destRaw,
                new RawTextureUAVKey().FromLevel(i));
            drawQueue.CommitUnorderedAccessViews();

            // Calculate number of threadgroups to dispatch
            var dispatchW = (destTexture.GetLevelWidth(i) + CubeFilterDispatchSize - 1) / CubeFilterDispatchSize;
            var dispatchH = (destTexture.GetLevelHeight(i) + CubeFilterDispatchSize - 1) / CubeFilterDispatchSize;
            drawQueue.Dispatch(new Vector3i(dispatchW, dispatchH, 6));
        }

        renderContext.ResetRenderTargets();
        renderContext.Execute(drawQueue);
    }

    private void EnsurePipelineStates(int numLevels)
    {
        if (_cachedPipelineStates is not null && _cachedPipelineStates.NumLevels == numLevels)
            return;

        var graphics = GetSubsystem<Graphics>();
        var pipelineStateCache = GetSubsystem<PipelineStateCache>();
        if (graphics is null || pipelineStateCache is null)
            return;

        var cache = new CachedPipelineStates { NumLevels = numLevels };

        var rayCounts = GetRayCounts(numLevels);
        var roughStep = 1.0f / (numLevels - 1);
        for (var i = 0; i < numLevels; ++i)
        {
            var levelWidth = (float)(1 << (numLevels - 1 - i));
            var rayCount = rayCounts[Math.Min(i, rayCounts.Length - 1)];

            var shaderParams = FormattableString.Invariant(
                $"RAY_COUNT={rayCount}u FILTER_RES={(uint)levelWidth}u FILTER_INV_RES={1.0f / levelWidth:F7} ROUGHNESS={roughStep * i:F7}");

            var desc = new ComputePipelineStateDesc
            {
                DebugName = $"C_FilterCubemap: {i} of {numLevels}",
                ComputeShader = graphics.GetShader(ShaderType.CS, "v2/C_FilterCubemap", shaderParams)
            };
            desc.Samplers.Add(new StringHash("SourceTexture"), SamplerStateDesc.Bilinear());

            var pipelineState = pipelineStateCache.GetComputePipelineState(desc);
            if (pipelineState is null || !pipelineState.IsValid)
            {
                Log.GetLogger().LogError("CubemapRenderer::FilterCubemap failed to create pipeline state");
                return;
            }

            cache.PipelineStates.Add(pipelineState);
        }

        _cachedPipelineStates = cache;
    }
}
