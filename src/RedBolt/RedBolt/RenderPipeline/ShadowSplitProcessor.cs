namespace RedBolt;

/// <summary>
/// Manages single shadow split parameters and shadow casters.
/// Spot lights always have one split.
/// Directions lights have one split per cascade.
/// Point lights always have six splits.
/// </summary>
public class ShadowSplitProcessor : IDisposable
{
    /// <summary>Immutable.</summary>
    private readonly LightProcessor _lightProcessor;
    private readonly WeakReference<Light> _light;
    private readonly int _splitIndex;
    private readonly RenderBackend _renderBackend;

    /// <summary>Internal cached objects.</summary>
    /// <remarks>
    /// C++ creates a freestanding <c>Node(context)</c>. Managed nodes require a scene owner,
    /// so a private temporary scene holds the shadow-camera node for the same lifetime.
    /// </remarks>
    private readonly Scene _shadowScene;
    private readonly Node _shadowCameraNode;
    private readonly Camera _shadowCamera;
    private readonly Frustum _splitFrustum = new();
    private readonly Polyhedron _frustumVolume = new();
    private DirectionalLightShadowCasterQuery? _directionalShadowCasterQuery;

    /// <summary>Frame-specific objects.</summary>
    private FloatRange _cascadeZRange;
    private FloatRange _focusedCascadeZRange;
    private readonly List<Drawable> _shadowCasters = [];

    private ShadowMapRegion _shadowMap;
    private float _shadowMapWorldSpaceTexelSize;
    private int _shadowMapPadding;

    /// <summary>Shadow casters.</summary>
    private readonly List<PipelineBatch> _unsortedShadowBatches = [];
    private readonly List<PipelineBatchByState> _sortedShadowBatches = [];
    private PipelineBatchGroup<PipelineBatchByState> _shadowBatches;

    public ShadowSplitProcessor(LightProcessor owner, int splitIndex)
    {
        _lightProcessor = owner;
        _light = new WeakReference<Light>(owner.Light);
        _splitIndex = splitIndex;
        _renderBackend = Light.GetSubsystem<Graphics>()!.GetRenderBackend();
        _shadowScene = new Scene();
        _shadowCameraNode = _shadowScene.CreateChild("ShadowCamera", temporary: true);
        _shadowCamera = _shadowCameraNode.CreateComponent<Camera>();
    }

    public void Dispose()
    {
        _shadowBatches = default;
        _sortedShadowBatches.Clear();
        _unsortedShadowBatches.Clear();
        _shadowMap = default;
        _shadowCasters.Clear();
        _shadowScene.Dispose();
    }

    /// <summary>Initialize split parameters.</summary>
    public void InitializeDirectional(DrawableProcessor drawableProcessor,
        FloatRange splitRange, IReadOnlyList<Drawable> litGeometries)
    {
        var cullCamera = drawableProcessor.FrameInfo.Camera!;
        var focusParameters = Light.GetShadowFocus();

        // Initialize split Z ranges
        _cascadeZRange = splitRange;
        _focusedCascadeZRange = focusParameters.Focus
            ? drawableProcessor.SceneZRange & _cascadeZRange
            : _cascadeZRange;

        // Initialize shadow camera
        InitializeBaseDirectionalCamera(cullCamera);

        var lightSpaceBoundingBox = GetSplitShadowBoundingBoxInLightSpace(drawableProcessor, litGeometries);
        _shadowCamera.SetFarClip(lightSpaceBoundingBox.Max.Z);

        AdjustDirectionalLightCamera(lightSpaceBoundingBox, 0.0f);
    }

    public void InitializeSpot()
    {
        var lightNode = Light.Node!;
        _shadowCameraNode.SetTransform(lightNode.GetWorldPosition(), lightNode.GetWorldRotation());
        _shadowCamera.SetNearClip(Light.GetShadowNearFarRatio() * Light.GetRange());
        _shadowCamera.SetFarClip(Light.GetRange());
        _shadowCamera.SetFov(Light.GetFov());
        _shadowCamera.SetAspectRatio(Light.GetAspectRatio());
        _shadowCamera.SetOrthographic(false);
        _shadowCamera.SetZoom(1.0f);
    }

    public void InitializePoint(CubeMapFace face)
    {
        // When making a shadowed point light, align the splits along X, Y and Z axes regardless of light rotation
        _shadowCameraNode.SetPosition(Light.Node!.GetWorldPosition());
        _shadowCameraNode.SetDirection(face switch
        {
            CubeMapFace.PositiveX => Vector3f.Right,
            CubeMapFace.NegativeX => Vector3f.Left,
            CubeMapFace.PositiveY => Vector3f.Up,
            CubeMapFace.NegativeY => Vector3f.Down,
            CubeMapFace.PositiveZ => Vector3f.Forward,
            _ => Vector3f.Back
        });
        _shadowCamera.SetNearClip(Light.GetShadowNearFarRatio() * Light.GetRange());
        _shadowCamera.SetFarClip(Light.GetRange());
        _shadowCamera.SetFov(90.0f);
        _shadowCamera.SetAspectRatio(1.0f);
        _shadowCamera.SetOrthographic(false);
        _shadowCamera.SetZoom(1.0f);
    }

    /// <summary>Process shadow casters.</summary>
    public void ProcessDirectionalShadowCasters(
        DrawableProcessor drawableProcessor, List<Drawable> shadowCastersBuffer)
    {
        _shadowCasters.Clear();
        _unsortedShadowBatches.Clear();
        _sortedShadowBatches.Clear();

        // Skip split if outside of the scene
        if (!drawableProcessor.SceneZRange.Intersect(_cascadeZRange))
            return;

        // Query shadow casters
        var frameInfo = drawableProcessor.FrameInfo;
        var cullCamera = frameInfo.Camera!;
        var octree = frameInfo.Octree!;

        _directionalShadowCasterQuery ??= new DirectionalLightShadowCasterQuery(
            shadowCastersBuffer, _shadowCamera.GetFrustum(), DrawableFlags.Geometry, Light,
            cullCamera.GetShadowViewMask());
        _directionalShadowCasterQuery.Result = shadowCastersBuffer;
        _directionalShadowCasterQuery.Frustum.CopyFrom(_shadowCamera.GetFrustum());
        _directionalShadowCasterQuery.ViewMask = cullCamera.GetShadowViewMask();
        octree.GetDrawables(_directionalShadowCasterQuery);

        // Preprocess shadow casters
        drawableProcessor.PreprocessShadowCasters(_shadowCasters, shadowCastersBuffer, _cascadeZRange, Light, _shadowCamera);
    }

    public void ProcessSpotShadowCasters(
        DrawableProcessor drawableProcessor, IReadOnlyList<Drawable> shadowCasterCandidates)
    {
        _shadowCasters.Clear();
        _unsortedShadowBatches.Clear();
        _sortedShadowBatches.Clear();

        // Preprocess shadow casters
        drawableProcessor.PreprocessShadowCasters(_shadowCasters, shadowCasterCandidates, default, Light, _shadowCamera);
    }

    public void ProcessPointShadowCasters(
        DrawableProcessor drawableProcessor, IReadOnlyList<Drawable> shadowCasterCandidates)
    {
        _shadowCasters.Clear();
        _unsortedShadowBatches.Clear();
        _sortedShadowBatches.Clear();

        // Check that the face is visible: if not, can skip the split
        var cullCamera = drawableProcessor.FrameInfo.Camera!;
        var cullCameraFrustum = cullCamera.GetFrustum();
        var shadowCameraFrustum = _shadowCamera.GetFrustum();

        if (cullCameraFrustum.IsInsideFast(new BoundingBox(shadowCameraFrustum)) == Intersection.Outside)
            return;

        // Preprocess shadow casters
        drawableProcessor.PreprocessShadowCasters(_shadowCasters, shadowCasterCandidates, default, Light, _shadowCamera);
    }

    public void FinalizeShadow(ShadowMapRegion shadowMap, int pcfKernelSize)
    {
        _shadowMap = shadowMap;

        var shadowMapWidth = (float)_shadowMap.Rect.Width;
        var lightType = Light.GetLightType();

        if (lightType == LightType.Directional)
        {
            var halfY = _shadowCamera.GetOrthoSize() * 0.5f;
            var halfX = _shadowCamera.GetAspectRatio() * halfY;
            var shadowBox = new BoundingBox(
                new Vector3f(-halfX, -halfY, 0.0f),
                new Vector3f(halfX, halfY, 0.0f));

            // Requantize and snap to shadow map texels
            AdjustDirectionalLightCamera(shadowBox, shadowMapWidth);
        }

        _shadowMapPadding = Math.Min(4, 1 + pcfKernelSize / 2);
        var effectiveShadowMapWidth = shadowMapWidth - 2.0f * _shadowMapPadding;
        _shadowCamera.SetZoom(effectiveShadowMapWidth / shadowMapWidth);

        // Estimate shadow map texel size. Exact size for directional light, upper bound for point and spot lights.
        var cameraSize = _shadowCamera.GetViewSizeAt(_shadowCamera.GetFarClip());
        _shadowMapWorldSpaceTexelSize = Math.Max(cameraSize.X, cameraSize.Y) / shadowMapWidth;
    }

    public void FinalizeShadowBatches()
    {
        BatchCompositor.FillSortKeys(_sortedShadowBatches, _unsortedShadowBatches);
        _sortedShadowBatches.Sort();
        _shadowBatches = new PipelineBatchGroup<PipelineBatchByState>
        {
            Batches = _sortedShadowBatches,
            Flags = BatchRenderFlag.EnableInstancingForStaticGeometry | BatchRenderFlag.DisableColorOutput
        };
    }

    /// <summary>Return immutable.</summary>
    public LightProcessor LightProcessor => _lightProcessor;

    public Light Light
    {
        get
        {
            _light.TryGetTarget(out var light);
            return light!;
        }
    }

    public int SplitIndex => _splitIndex;

    /// <summary>Return values are valid after shadow casters are processed.</summary>
    public IReadOnlyList<Drawable> ShadowCasters => _shadowCasters;

    public bool HasShadowCasters => _shadowCasters.Count > 0;

    /// <summary>Return values are valid after shadow map is finalized.</summary>
    public Matrix4 GetWorldToShadowSpaceMatrix(float subPixelOffset)
    {
        if (!(bool)_shadowMap)
            return Matrix4.Identity;

        var viewport = _shadowMap.Rect;
        var shadowView = _shadowCamera.GetView();
        var shadowProj = _shadowCamera.GetGPUProjection();
        var textureSize = _shadowMap.Texture!.GetSize();

        var offset = new Vector3f(
            (float)viewport.Left / textureSize.X,
            (float)viewport.Top / textureSize.Y,
            0.0f);
        var scale = new Vector3f(
            0.5f * viewport.Width / textureSize.X,
            0.5f * viewport.Height / textureSize.Y,
            1.0f);

        offset.X += scale.X;
        offset.Y += scale.Y;

        // Apply GAPI-specific transforms
        if (_renderBackend == RenderBackend.OpenGL)
        {
            offset.Z = 0.5f;
            scale.Z = 0.5f;
            offset.Y = 1.0f - offset.Y;
        }
        else
        {
            scale.Y = -scale.Y;
        }

        // Apply sub-pixel offset if necessary
        offset.X -= subPixelOffset / textureSize.X;
        offset.Y -= subPixelOffset / textureSize.Y;

        // Make final matrix
        var texAdjust = Matrix4.Identity;
        texAdjust.SetTranslation(offset);
        texAdjust.SetScale(scale);

        return texAdjust * shadowProj * shadowView.Matrix4;
    }

    public ref readonly ShadowMapRegion ShadowMap => ref _shadowMap;

    public float ShadowMapTexelSizeInWorldSpace => _shadowMapWorldSpaceTexelSize;

    public int ShadowMapPadding => _shadowMapPadding;

    public ref readonly FloatRange CascadeZRange => ref _cascadeZRange;

    public Camera ShadowCamera => _shadowCamera;

    public List<PipelineBatch> MutableUnsortedShadowBatches => _unsortedShadowBatches;

    public ref PipelineBatchGroup<PipelineBatchByState> MutableShadowBatches => ref _shadowBatches;

    public ref readonly PipelineBatchGroup<PipelineBatchByState> ShadowBatches => ref _shadowBatches;

    private void InitializeBaseDirectionalCamera(Camera cullCamera)
    {
        var lightNode = Light.Node!;
        var extrusionDistance = Math.Min(cullCamera.GetFarClip(), Light.GetShadowMaxExtrusion());

        var position = cullCamera.Node!.GetWorldPosition() - extrusionDistance * lightNode.GetWorldDirection();
        _shadowCameraNode.SetTransform(position, lightNode.GetWorldRotation());

        _shadowCamera.SetOrthographic(true);
        _shadowCamera.SetAspectRatio(1.0f);
        _shadowCamera.SetNearClip(0.0f);
        _shadowCamera.SetZoom(1.0f);
    }

    private BoundingBox GetLitGeometriesBoundingBox(
        DrawableProcessor drawableProcessor, IReadOnlyList<Drawable> litGeometries)
    {
        var litGeometriesBox = new BoundingBox();
        for (var i = 0; i < litGeometries.Count; ++i)
        {
            var drawable = litGeometries[i];
            var geometryZRange = drawableProcessor.GetGeometryZRange(checked((int)drawable.GetDrawableIndex()));
            if (geometryZRange.Intersect(_cascadeZRange))
                litGeometriesBox.Merge(drawable.GetWorldBoundingBox());
        }

        return litGeometriesBox;
    }

    private BoundingBox GetSplitShadowBoundingBoxInLightSpace(
        DrawableProcessor drawableProcessor, IReadOnlyList<Drawable> litGeometries)
    {
        var cullCamera = drawableProcessor.FrameInfo.Camera!;
        var focusParameters = Light.GetShadowFocus();
        cullCamera.GetSplitFrustum(_focusedCascadeZRange.First, _focusedCascadeZRange.Second, _splitFrustum);

        _frustumVolume.Define(_splitFrustum);

        // Focus frustum volume onto lit geometries
        // If volume became empty, restore it to avoid zero size
        if (focusParameters.Focus)
        {
            var litGeometriesBox = GetLitGeometriesBoundingBox(drawableProcessor, litGeometries);

            if (litGeometriesBox.Defined)
            {
                // Always keep small padding to ensure stable clipping
                litGeometriesBox.Min -= Vector3f.One;
                litGeometriesBox.Max += Vector3f.One;

                _frustumVolume.Clip(litGeometriesBox);
                if (_frustumVolume.Empty())
                    _frustumVolume.Define(_splitFrustum);
            }
        }

        // Transform frustum volume to light space
        var lightView = _shadowCamera.GetView();
        _frustumVolume.Transform(lightView);

        // Fit the frustum volume inside a bounding box. If uniform size, use a sphere instead
        BoundingBox shadowBox;
        if (!focusParameters.NonUniform)
            shadowBox = new BoundingBox(new Sphere(_frustumVolume));
        else
            shadowBox = new BoundingBox(_frustumVolume);
        return shadowBox;
    }

    private void AdjustDirectionalLightCamera(BoundingBox lightSpaceBoundingBox, float shadowMapSize)
    {
        var focusParameters = Light.GetShadowFocus();

        // Evaluate shadow split rectangle in light space
        var lightSpaceRect = new Rect(lightSpaceBoundingBox.Min.ToVector2(), lightSpaceBoundingBox.Max.ToVector2());
        var center = lightSpaceRect.Center;
        var viewSize = CalculateViewSize(lightSpaceRect.Size, focusParameters);
        _shadowCamera.SetOrthoSize(viewSize);

        // Center shadow camera to the light space
        var lightRotation = _shadowCameraNode.GetWorldRotation();
        _shadowCameraNode.Translate(lightRotation * center.ToVector3(), TransformSpace.World);

        // If the shadow map viewport is known, snap to whole texels
        if (shadowMapSize > 0.0f)
        {
            var lightSpacePosition = lightRotation.Inverse * _shadowCameraNode.GetWorldPosition();
            // Take into account that shadow map border will not be used
            var invActualSize = 1.0f / (shadowMapSize - 2.0f);
            var texelSize = new Vector2f(viewSize.X * invActualSize, viewSize.Y * invActualSize);
            // C++ uses fmodf
            var snap = new Vector3f(
                -Fmod(lightSpacePosition.X, texelSize.X),
                -Fmod(lightSpacePosition.Y, texelSize.Y),
                0.0f);
            _shadowCameraNode.Translate(lightRotation * snap, TransformSpace.World);
        }
    }

    /// <summary>Calculate view size for given min view size and light parameters.</summary>
    private static Vector2f CalculateViewSize(Vector2f minViewSize, FocusParameters parameters)
    {
        // Quantize size to reduce swimming
        // Convert view scale with non-linear function for adaptive scale
        if (parameters.NonUniform)
        {
            var adaptiveViewSize = Vector2f.Ceil(Vector2f.Sqrt(minViewSize / parameters.Quantize));
            var snappedViewSize = adaptiveViewSize * adaptiveViewSize * parameters.Quantize;
            return Vector2f.Max(snappedViewSize, new Vector2f(parameters.MinView, parameters.MinView));
        }

        if (parameters.Focus)
        {
            var maxViewSize = Math.Max(minViewSize.X, minViewSize.Y);
            var adaptiveViewSize = MathF.Ceiling(MathF.Sqrt(maxViewSize / parameters.Quantize));
            var snappedViewSize = Math.Max(adaptiveViewSize * adaptiveViewSize * parameters.Quantize, parameters.MinView);
            return new Vector2f(snappedViewSize, snappedViewSize);
        }

        return minViewSize;
    }

    private static float Fmod(float x, float y) => x - y * MathF.Truncate(x / y);
}
