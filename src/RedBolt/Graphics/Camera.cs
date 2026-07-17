namespace RedBolt;

[Flags]
public enum ViewOverride : uint
{
    None = 0,
    LowMaterialQuality = 1 << 0,
    DisableShadows = 1 << 1,
    DisableOcclusion = 1 << 2
}

public sealed class Camera : Component
{
    public const uint DefaultViewMask = uint.MaxValue;
    public const uint DefaultZoneMask = uint.MaxValue;

    private const float DefaultFarClip = 1000.0f;
    private const float DefaultFov = 45.0f;
    private const float DefaultNearClip = 0.1f;
    private const float DefaultOrthoSize = 20.0f;
    private const float MaxFov = 160.0f;
    private static readonly Matrix4 FlipMatrix = new(
        1.0f, 0.0f, 0.0f, 0.0f,
        0.0f, -1.0f, 0.0f, 0.0f,
        0.0f, 0.0f, 1.0f, 0.0f,
        0.0f, 0.0f, 0.0f, 1.0f);

    private float _aspectRatio = 1.0f;
    private float _farClip = DefaultFarClip;
    private float _fov = DefaultFov;
    private float _lodBias = 1.0f;
    private float _nearClip = DefaultNearClip;
    private float _orthoSize = DefaultOrthoSize;
    private float _zoom = 1.0f;
    private Plane _reflectionPlane = Plane.Up;
    private Plane _clipPlane = Plane.Up;
    private Matrix3x4 _reflectionMatrix = Matrix3x4.Identity;
    private float _projectionNearClip;
    private float _projectionFarClip;
    private bool _customProjectionEnabled;
    private bool _projectionDirty = true;
    private bool _viewDirty = true;
    private bool _viewProjDirty = true;
    private bool _frustumDirty = true;
    private Matrix4 _projection;
    private Matrix4 _inverseProjection;
    private Matrix3x4 _view;
    private Matrix3x4 _inverseView;
    private Matrix4 _viewProj;
    private Matrix4 _inverseViewProj;
    private readonly Frustum _frustum = new();
    private readonly WeakReference<Graphics>? _graphics;
    private WeakReference<Zone>? _zone;

    public Camera()
    {
        var graphics = GetSubsystem<Graphics>();
        _graphics = graphics is not null ? new WeakReference<Graphics>(graphics) : null;
        RegisterAttribute("Near Clip", typeof(float), self => ((Camera)self).GetNearClip(), (self, value) => ((Camera)self).SetNearClip((float)value!), DefaultNearClip);
        RegisterAttribute("Far Clip", typeof(float), self => ((Camera)self).GetFarClip(), (self, value) => ((Camera)self).SetFarClip((float)value!), DefaultFarClip);
        RegisterAttribute("FOV", typeof(float), self => ((Camera)self).GetFov(), (self, value) => ((Camera)self).SetFov((float)value!), DefaultFov);
        RegisterAttribute("Aspect Ratio", typeof(float), self => ((Camera)self).GetAspectRatio(), (self, value) => ((Camera)self).SetAspectRatioInternal((float)value!), 1.0f);
        RegisterAttribute("Fill Mode", typeof(FillMode), self => ((Camera)self).GetFillMode(), (self, value) => ((Camera)self).SetFillMode((FillMode)value!), FillMode.Solid);
        RegisterAttribute("Auto Aspect Ratio", typeof(bool), self => ((Camera)self).GetAutoAspectRatio(), (self, value) => ((Camera)self).SetAutoAspectRatio(value is bool enabled && enabled), true);
        RegisterAttribute("Orthographic", typeof(bool), self => ((Camera)self).IsOrthographic(), (self, value) => ((Camera)self).SetOrthographic(value is bool enabled && enabled), false);
        RegisterAttribute("Orthographic Size", typeof(float), self => ((Camera)self).GetOrthoSize(), (self, value) => ((Camera)self).SetOrthoSizeAttr((float)value!), DefaultOrthoSize);
        RegisterAttribute("Zoom", typeof(float), self => ((Camera)self).GetZoom(), (self, value) => ((Camera)self).SetZoom((float)value!), 1.0f);
        RegisterAttribute("LOD Bias", typeof(float), self => ((Camera)self).GetLodBias(), (self, value) => ((Camera)self).SetLodBias((float)value!), 1.0f);
        RegisterAttribute("View Mask", typeof(uint), self => ((Camera)self).GetViewMask(), (self, value) => ((Camera)self).SetViewMask((uint)value!), DefaultViewMask);
        RegisterAttribute("Primary View Mask", typeof(uint), self => ((Camera)self).GetPrimaryViewMask(), (self, value) => ((Camera)self).SetPrimaryViewMask((uint)value!), DefaultViewMask);
        RegisterAttribute("Shadow View Mask", typeof(uint), self => ((Camera)self).GetShadowViewMask(), (self, value) => ((Camera)self).SetShadowViewMask((uint)value!), DefaultViewMask);
        RegisterAttribute("Zone Mask", typeof(uint), self => ((Camera)self).GetZoneMask(), (self, value) => ((Camera)self).SetZoneMask((uint)value!), DefaultZoneMask);
        RegisterAttribute("View Override Flags", typeof(ViewOverride), self => ((Camera)self).GetViewOverrideFlags(), (self, value) => ((Camera)self).SetViewOverrideFlags((ViewOverride)value!), ViewOverride.None);
        RegisterAttribute("Projection Offset", typeof(Vector2f), self => ((Camera)self).GetProjectionOffset(), (self, value) => ((Camera)self).SetProjectionOffset((Vector2f)value!), Vector2f.Zero);
        RegisterAttribute("Reflection Plane", typeof(Vector4f), self => ((Camera)self).GetReflectionPlaneAttr(), (self, value) => ((Camera)self).SetReflectionPlaneAttr((Vector4f)value!), Plane.Up.Vector4f);
        RegisterAttribute("Clip Plane", typeof(Vector4f), self => ((Camera)self).GetClipPlaneAttr(), (self, value) => ((Camera)self).SetClipPlaneAttr((Vector4f)value!), Plane.Up.Vector4f);
        RegisterAttribute("Use Reflection", typeof(bool), self => ((Camera)self).GetUseReflection(), (self, value) => ((Camera)self).SetUseReflection(value is bool enabled && enabled), false);
        RegisterAttribute("Use Clipping", typeof(bool), self => ((Camera)self).GetUseClipping(), (self, value) => ((Camera)self).SetUseClipping(value is bool enabled && enabled), false);
        RegisterAttribute("Draw Debug Geometry", typeof(bool), self => ((Camera)self).GetDrawDebugGeometry(), (self, value) => ((Camera)self).SetDrawDebugGeometry(value is bool enabled && enabled), true);
    }

    public float AspectRatio
    {
        get => GetAspectRatio();
        set => SetAspectRatio(value);
    }

    public bool AutoAspectRatio { get; set; } = true;

    public bool DrawDebugGeometryEnabled { get; set; } = true;

    public Plane ClipPlane => _clipPlane;

    public Color EffectiveAmbientColor => GetEffectiveAmbientColor();

    public float EffectiveAmbientBrightness => GetEffectiveAmbientBrightness();

    public Color EffectiveFogColor => GetEffectiveFogColor();

    public float EffectiveFogStart => GetEffectiveFogStart();

    public float EffectiveFogEnd => GetEffectiveFogEnd();

    public float FarClip
    {
        get => GetFarClip();
        set => SetFarClip(value);
    }

    public FillMode FillMode { get; set; } = FillMode.Solid;

    public bool FlipVertical { get; set; }

    public float FieldOfView
    {
        get => GetFov();
        set => SetFov(value);
    }

    public float Fov
    {
        get => GetFov();
        set => SetFov(value);
    }

    public float LodBias
    {
        get => GetLodBias();
        set => SetLodBias(value);
    }

    public Vector2f MousePosition { get; private set; }

    public float NearClip
    {
        get => GetNearClip();
        set => SetNearClip(value);
    }

    public bool Orthographic { get; set; }

    public float OrthoSize
    {
        get => GetOrthoSize();
        set => SetOrthoSize(value);
    }

    public uint PrimaryViewMask { get; set; } = DefaultViewMask;

    public Vector2f ProjectionOffset { get; set; }

    public uint ShadowViewMask { get; set; } = DefaultViewMask;

    public Plane ReflectionPlane => _reflectionPlane;

    public Zone? Zone => GetZone();

    public bool UseClipping { get; set; }

    public bool UseReflection { get; set; }

    public uint ViewMask => GetViewMask();

    public ViewOverride ViewOverrideFlags { get; set; }

    public uint ZoneMask { get; set; } = DefaultZoneMask;

    public float Zoom
    {
        get => GetZoom();
        set => SetZoom(value);
    }

    public float GetAspectRatio() => _aspectRatio;

    public bool GetAutoAspectRatio() => AutoAspectRatio;

    public bool GetDrawDebugGeometry() => DrawDebugGeometryEnabled;

    public override void DrawDebugGeometry(DebugRenderer? debug, bool depthTest)
    {
        debug?.AddFrustum(GetFrustum(), Color.White, depthTest);
    }

    /// <summary>
    /// Return frustum in world space.
    /// </summary>
    public Frustum GetFrustum()
    {
        if (!_frustumDirty)
            return _frustum;

        if (_projectionDirty)
            UpdateProjection();

        if (_customProjectionEnabled)
            _frustum.Define(GetProjection(ignoreFlip: true) * GetView().Matrix4);
        else if (!Orthographic)
            _frustum.Define(Fov, AspectRatio, Zoom, GetNearClip(), GetFarClip(), GetEffectiveWorldTransform());
        else
            _frustum.DefineOrtho(OrthoSize, AspectRatio, Zoom, GetNearClip(), GetFarClip(), GetEffectiveWorldTransform());

        _frustumDirty = false;
        return _frustum;
    }

    public Frustum GetSplitFrustum(float nearClip, float farClip)
    {
        if (_projectionDirty)
            UpdateProjection();

        nearClip = Math.Max(nearClip, _projectionNearClip);
        farClip = Math.Min(farClip, _projectionFarClip);
        if (farClip < nearClip)
            farClip = nearClip;

        var result = new Frustum();
        if (_customProjectionEnabled)
        {
            result.DefineSplit(GetProjection(ignoreFlip: true), nearClip, farClip);
            result.Transform(GetEffectiveWorldTransform());
        }
        else if (!Orthographic)
            result.Define(Fov, AspectRatio, Zoom, nearClip, farClip, GetEffectiveWorldTransform());
        else
            result.DefineOrtho(OrthoSize, AspectRatio, Zoom, nearClip, farClip, GetEffectiveWorldTransform());

        return result;
    }

    public Frustum GetViewSpaceFrustum()
    {
        if (_projectionDirty)
            UpdateProjection();

        var result = new Frustum();
        if (_customProjectionEnabled)
            result.Define(GetProjection(ignoreFlip: true));
        else if (!Orthographic)
            result.Define(Fov, AspectRatio, Zoom, GetNearClip(), GetFarClip());
        else
            result.DefineOrtho(OrthoSize, AspectRatio, Zoom, GetNearClip(), GetFarClip());
        return result;
    }

    public Frustum GetViewSpaceSplitFrustum(float nearClip, float farClip)
    {
        if (_projectionDirty)
            UpdateProjection();

        nearClip = Math.Max(nearClip, _projectionNearClip);
        farClip = Math.Min(farClip, _projectionFarClip);
        if (farClip < nearClip)
            farClip = nearClip;

        var result = new Frustum();
        if (_customProjectionEnabled)
            result.DefineSplit(GetProjection(ignoreFlip: true), nearClip, farClip);
        else if (!Orthographic)
            result.Define(Fov, AspectRatio, Zoom, nearClip, farClip);
        else
            result.DefineOrtho(OrthoSize, AspectRatio, Zoom, nearClip, farClip);
        return result;
    }

    public void GetFrustumSize(out Vector3f nearSize, out Vector3f farSize)
    {
        var viewSpaceFrustum = GetViewSpaceFrustum();
        nearSize = viewSpaceFrustum.Vertices[0];
        farSize = viewSpaceFrustum.Vertices[4];

        if (FlipVertical)
        {
            nearSize.Y = -nearSize.Y;
            farSize.Y = -farSize.Y;
        }
    }

    public float GetDistance(Vector3f worldPos)
    {
        if (!Orthographic)
        {
            var cameraPos = IsAttached ? Node.WorldPosition : Vector3f.Zero;
            return (worldPos - cameraPos).Length;
        }

        var viewPos = GetView().Matrix4 * new Vector4f(worldPos, 1.0f);
        return Math.Abs(viewPos.Z);
    }

    public float GetDistanceSquared(Vector3f worldPos)
    {
        if (!Orthographic)
        {
            var cameraPos = IsAttached ? Node.WorldPosition : Vector3f.Zero;
            return (worldPos - cameraPos).LengthSquared;
        }

        var viewPos = GetView().Matrix4 * new Vector4f(worldPos, 1.0f);
        return viewPos.Z * viewPos.Z;
    }

    public float GetFarClip()
    {
        if (_projectionDirty)
            UpdateProjection();
        return _projectionFarClip;
    }

    public FillMode GetFillMode() => FillMode;

    public bool GetFlipVertical() => FlipVertical;

    public float GetFov() => _fov;

    public float GetHalfViewSize() => !Orthographic
        ? MathF.Tan(_fov * MathDefs.DegToRad * 0.5f) / _zoom
        : _orthoSize * 0.5f / _zoom;

    public float GetLodBias() => _lodBias;

    public float GetLodDistance(float distance, float scale, float bias)
    {
        var divisor = Math.Max(_lodBias * bias * scale * _zoom, MathDefs.Epsilon);
        return !Orthographic ? distance / divisor : _orthoSize / divisor;
    }

    public float GetNearClip()
    {
        if (_projectionDirty)
            UpdateProjection();
        return _projectionNearClip;
    }

    public Matrix3x4 GetView()
    {
        if (_viewDirty)
            UpdateView();
        return _view;
    }

    public Matrix3x4 GetInverseView()
    {
        if (_viewDirty)
            UpdateView();
        return _inverseView;
    }

    public Matrix4 GetInverseProjection(bool ignoreFlip = false)
    {
        if (_projectionDirty)
            UpdateProjection();
        return FlipVertical && !ignoreFlip ? _inverseProjection * FlipMatrix : _inverseProjection;
    }

    public Matrix4 GetProjection(bool ignoreFlip = false)
    {
        if (_projectionDirty)
            UpdateProjection();
        return FlipVertical && !ignoreFlip ? FlipMatrix * _projection : _projection;
    }

    public Matrix4 GetGPUProjection(bool ignoreFlip = false)
    {
        var projection = GetProjection(ignoreFlip);
        var isOpenGL = _graphics is not null && _graphics.TryGetTarget(out var graphics)
            && graphics.GetRenderBackend() == RenderBackend.OpenGL;
        if (!isOpenGL)
            return projection;

        projection.M20 = 2.0f * projection.M20 - projection.M30;
        projection.M21 = 2.0f * projection.M21 - projection.M31;
        projection.M22 = 2.0f * projection.M22 - projection.M32;
        projection.M23 = 2.0f * projection.M23 - projection.M33;
        return projection;
    }

    public Matrix4 GetEffectiveGPUViewProjection(float constantDepthBias)
    {
        var projection = GetGPUProjection();
        var isOpenGL = _graphics is not null && _graphics.TryGetTarget(out var graphics)
            && graphics.GetRenderBackend() == RenderBackend.OpenGL;
        if (isOpenGL)
        {
            var constantBias = 2.0f * constantDepthBias;
            projection.M22 += projection.M32 * constantBias;
            projection.M23 += projection.M33 * constantBias;
        }

        return projection * GetView().Matrix4;
    }

    public Matrix4 GetViewProj()
    {
        if (_viewProjDirty)
            UpdateViewProjectionMatrices();
        return _viewProj;
    }

    public Matrix4 GetGPUViewProj() => GetGPUProjection() * GetView().Matrix4;

    public Matrix4 GetInverseViewProj()
    {
        if (_viewProjDirty)
            UpdateViewProjectionMatrices();
        return _inverseViewProj;
    }

    public Ray GetScreenRay(float x, float y)
    {
        if (!IsProjectionValid())
        {
            var result = new Ray
            {
                Origin = IsAttached ? Node.WorldPosition : Vector3f.Zero,
                Direction = IsAttached ? Node.WorldDirection : Vector3f.Forward
            };
            return result;
        }

        var viewProjInverse = (GetProjection() * GetView().Matrix4).Inverted;
        x = 2.0f * x - 1.0f;
        y = 1.0f - 2.0f * y;
        var nearPlane = new Vector3f(x, y, 0.0f);
        var farPlane = new Vector3f(x, y, 1.0f);

        var resultRay = new Ray
        {
            Origin = viewProjInverse * nearPlane
        };
        resultRay.Direction = (viewProjInverse * farPlane - resultRay.Origin).Normalized;
        return resultRay;
    }

    public Ray GetScreenRayFromMouse()
    {
        if (!HasMousePosition())
            return default;
        return GetScreenRay(MousePosition.X, MousePosition.Y);
    }

    public Vector2f WorldToScreenPoint(Vector3f worldPos)
    {
        var eyeSpacePos = GetView().Matrix4 * new Vector4f(worldPos, 1.0f);
        var result = new Vector2f();
        if (eyeSpacePos.Z > 0.0f)
        {
            var screenSpacePos = GetProjection() * new Vector3f(eyeSpacePos.X, eyeSpacePos.Y, eyeSpacePos.Z);
            result.X = screenSpacePos.X;
            result.Y = screenSpacePos.Y;
        }
        else
        {
            result.X = -eyeSpacePos.X > 0.0f ? -1.0f : 1.0f;
            result.Y = -eyeSpacePos.Y > 0.0f ? -1.0f : 1.0f;
        }

        result.X = result.X / 2.0f + 0.5f;
        result.Y = 1.0f - (result.Y / 2.0f + 0.5f);
        return result;
    }

    public Vector3f ScreenToWorldPoint(Vector3f screenPos)
    {
        var ray = GetScreenRay(screenPos.X, screenPos.Y);
        var viewSpaceDirection = GetView().Matrix4 * new Vector4f(ray.Direction, 0.0f);
        var rayDistance = Math.Max(screenPos.Z - GetNearClip(), 0.0f) / viewSpaceDirection.Z;
        return ray.Origin + ray.Direction * rayDistance;
    }

    public float GetOrthoSize() => _orthoSize;

    public uint GetPrimaryViewMask() => PrimaryViewMask;

    public Vector2f GetProjectionOffset() => ProjectionOffset;

    public bool GetReverseCulling() => FlipVertical ^ UseReflection;

    public Plane GetReflectionPlane() => _reflectionPlane;

    public Plane GetClipPlane() => _clipPlane;

    public Zone? GetZone() => _zone is not null && _zone.TryGetTarget(out var zone) ? zone : null;

    public Color GetEffectiveAmbientColor() => GetZone()?.GetAmbientColor() ?? Color.Transparent;

    public float GetEffectiveAmbientBrightness() => GetZone()?.GetAmbientBrightness() ?? 1.0f;

    public Color GetEffectiveFogColor() => GetZone()?.GetFogColor() ?? Color.Transparent;

    public float GetEffectiveFogStart() => GetZone()?.GetFogStart() ?? MathDefs.LargeValue;

    public float GetEffectiveFogEnd() => GetZone()?.GetFogEnd() ?? MathDefs.LargeValue;

    public Quaternion GetFaceCameraRotation(
        Vector3f position, Quaternion rotation, FaceCameraMode mode, float minAngle = 0.0f)
    {
        if (!IsAttached)
            return rotation;

        switch (mode)
        {
            case FaceCameraMode.RotateXyz:
                return Node.WorldRotation;

            case FaceCameraMode.RotateY:
            {
                var euler = rotation.EulerAngles;
                euler.Y = Node.WorldRotation.EulerAngles.Y;
                return new Quaternion(euler.X, euler.Y, euler.Z);
            }

            case FaceCameraMode.LookAtXyz:
            {
                Quaternion.LookRotation(position - Node.WorldPosition, Vector3f.Up, out var lookAt);
                return lookAt;
            }

            case FaceCameraMode.LookAtY:
            case FaceCameraMode.LookAtMixed:
            {
                var lookAtVector = position - Node.WorldPosition;
                var lookAtVectorXz = new Vector3f(lookAtVector.X, 0.0f, lookAtVector.Z);
                Quaternion.LookRotation(lookAtVectorXz, Vector3f.Up, out var lookAt);

                var euler = rotation.EulerAngles;
                if (mode == FaceCameraMode.LookAtMixed)
                {
                    var angle = lookAtVector.Angle(rotation * Vector3f.Up);
                    if (angle > 180.0f - minAngle)
                        euler.X += minAngle - (180.0f - angle);
                    else if (angle < minAngle)
                        euler.X -= minAngle - angle;
                }

                euler.Y = lookAt.EulerAngles.Y;
                return new Quaternion(euler.X, euler.Y, euler.Z);
            }

            default:
                return rotation;
        }
    }

    public uint GetShadowViewMask() => ShadowViewMask;

    public bool GetUseClipping() => UseClipping;

    public bool GetUseReflection() => UseReflection;

    public Vector2f GetViewSizeAt(float z)
    {
        var halfHeight = GetHalfViewSize();
        var halfSize = new Vector2f(_aspectRatio * halfHeight, halfHeight);
        return Orthographic ? halfSize : halfSize * z;
    }

    public uint GetViewMask() => PrimaryViewMask | ShadowViewMask;

    public ViewOverride GetViewOverrideFlags() => ViewOverrideFlags;

    public float GetZoom() => _zoom;

    public uint GetZoneMask() => ZoneMask;

    public bool HasMousePosition() => MousePosition.X >= 0.0f && MousePosition.X <= 1.0f && MousePosition.Y >= 0.0f && MousePosition.Y <= 1.0f;

    public bool IsOrthographic() => Orthographic;

    public bool IsProjectionValid() => GetFarClip() > GetNearClip();

    public void SetAspectRatio(float value)
    {
        AutoAspectRatio = false;
        SetAspectRatioInternal(value);
    }

    public void SetAspectRatioInternal(float value)
    {
        if (_aspectRatio != value)
        {
            _aspectRatio = value;
            InvalidateProjection();
        }
    }

    public void SetAutoAspectRatio(bool enable)
    {
        AutoAspectRatio = enable;
    }

    public void SetDrawDebugGeometry(bool enable)
    {
        DrawDebugGeometryEnabled = enable;
    }

    public void SetFarClip(float value)
    {
        _farClip = Math.Max(value, MathDefs.MinNearClip);
        InvalidateProjection();
    }

    public void SetFillMode(FillMode mode)
    {
        FillMode = mode;
    }

    public void SetFlipVertical(bool enable)
    {
        FlipVertical = enable;
        _viewProjDirty = true;
    }

    public void SetFov(float value)
    {
        _fov = MathDefs.Clamp(value, 0.0f, MaxFov);
        InvalidateProjection();
    }

    public void SetLodBias(float value)
    {
        _lodBias = Math.Max(value, MathDefs.Epsilon);
    }

    public void SetMousePosition(Vector2f position)
    {
        MousePosition = position;
    }

    public void SetNearClip(float value)
    {
        _nearClip = Math.Max(value, MathDefs.MinNearClip);
        InvalidateProjection();
    }

    public void SetOrthographic(bool enable)
    {
        if (Orthographic != enable)
        {
            Orthographic = enable;
            InvalidateProjection();
        }
    }

    public void SetOrthoSize(float value)
    {
        if (_orthoSize != value || _aspectRatio != 1.0f)
        {
            _orthoSize = value;
            _aspectRatio = 1.0f;
            InvalidateProjection();
        }
    }

    public void SetOrthoSizeAttr(float value)
    {
        _orthoSize = value;
        InvalidateProjection();
    }

    public void SetOrthoSize(Vector2f value)
    {
        Orthographic = true;
        AutoAspectRatio = false;
        _orthoSize = value.Y;
        _aspectRatio = value.X / value.Y;
        InvalidateProjection();
    }

    public void SetPrimaryViewMask(uint mask)
    {
        PrimaryViewMask = mask;
    }

    public void SetProjectionOffset(Vector2f offset)
    {
        ProjectionOffset = offset;
        InvalidateProjection();
    }

    public void SetShadowViewMask(uint mask)
    {
        ShadowViewMask = mask;
    }

    public void SetUseClipping(bool enable)
    {
        UseClipping = enable;
        InvalidateProjection();
    }

    public void SetUseReflection(bool enable)
    {
        UseReflection = enable;
        _viewDirty = true;
        _viewProjDirty = true;
        _frustumDirty = true;
    }

    public void SetReflectionPlane(Plane plane)
    {
        _reflectionPlane = plane;
        _reflectionMatrix = plane.ReflectionMatrix();
        _viewDirty = true;
        _viewProjDirty = true;
        _frustumDirty = true;
    }

    public void SetClipPlane(Plane plane)
    {
        _clipPlane = plane;
    }

    public void SetReflectionPlaneAttr(Vector4f value)
    {
        SetReflectionPlane(new Plane(value));
    }

    public Vector4f GetReflectionPlaneAttr() => _reflectionPlane.Vector4f;

    public void SetClipPlaneAttr(Vector4f value)
    {
        SetClipPlane(new Plane(value));
    }

    public Vector4f GetClipPlaneAttr() => _clipPlane.Vector4f;

    public void SetViewMask(uint mask)
    {
        PrimaryViewMask = mask;
        ShadowViewMask = mask;
    }

    public void SetViewOverrideFlags(ViewOverride flags)
    {
        ViewOverrideFlags = flags;
    }

    public void SetZoom(float value)
    {
        _zoom = Math.Max(value, MathDefs.Epsilon);
        InvalidateProjection();
    }

    public void SetZoneMask(uint mask)
    {
        ZoneMask = mask;
    }

    public void SetZone(Zone? zone)
    {
        _zone = zone is not null ? new WeakReference<Zone>(zone) : null;
    }

    public void SetProjection(Matrix4 projection)
    {
        var projectionInverse = projection.Inverted;
        _projection = projection;
        _inverseProjection = projectionInverse;
        _projectionNearClip = (projectionInverse * new Vector3f(0.0f, 0.0f, 0.0f)).Z;
        _projectionFarClip = (projectionInverse * new Vector3f(0.0f, 0.0f, 1.0f)).Z;
        _customProjectionEnabled = true;
        AutoAspectRatio = false;
        _projectionDirty = false;
        _viewProjDirty = true;
        _frustumDirty = true;
    }

    public Matrix3x4 GetEffectiveWorldTransform()
    {
        var worldTransform = IsAttached
            ? new Matrix3x4(Node.WorldPosition, Node.WorldRotation, 1.0f)
            : Matrix3x4.Identity;
        return UseReflection ? _reflectionMatrix * worldTransform : worldTransform;
    }

    protected override void OnNodeSet(Node? previousNode, Node? currentNode)
    {
        if (currentNode is not null)
            currentNode.AddListener(this);
        InvalidateView();
    }

    protected internal override void OnMarkedDirty(Node node)
    {
        InvalidateView();
    }

    private void UpdateProjection()
    {
        if (_customProjectionEnabled)
        {
            _projectionDirty = false;
            return;
        }

        var projection = Matrix4.Zero;
        if (!Orthographic)
        {
            var h = 1.0f / MathF.Tan(_fov * MathDefs.DegToRad * 0.5f) * _zoom;
            var w = h / _aspectRatio;
            var q = _farClip / (_farClip - _nearClip);
            var r = -q * _nearClip;

            projection.M00 = w;
            projection.M02 = ProjectionOffset.X * 2.0f;
            projection.M11 = h;
            projection.M12 = ProjectionOffset.Y * 2.0f;
            projection.M22 = q;
            projection.M23 = r;
            projection.M32 = 1.0f;
            _projectionNearClip = _nearClip;
            _projectionFarClip = _farClip;
        }
        else
        {
            var h = 1.0f / (_orthoSize * 0.5f) * _zoom;
            var w = h / _aspectRatio;
            var q = 1.0f / _farClip;

            projection.M00 = w;
            projection.M03 = ProjectionOffset.X * 2.0f;
            projection.M11 = h;
            projection.M13 = ProjectionOffset.Y * 2.0f;
            projection.M22 = q;
            projection.M33 = 1.0f;
            _projectionNearClip = 0.0f;
            _projectionFarClip = _farClip;
        }

        _projection = projection;
        _inverseProjection = projection.Inverted;
        _projectionDirty = false;
    }

    private void UpdateView()
    {
        var worldTransform = GetEffectiveWorldTransform();
        _view = worldTransform.Inverse;
        _inverseView = worldTransform;
        _viewDirty = false;
    }

    private void UpdateViewProjectionMatrices()
    {
        _viewProj = GetProjection() * GetView().Matrix4;
        _inverseViewProj = _viewProj.Inverted;
        _viewProjDirty = false;
    }

    private void InvalidateProjection()
    {
        _customProjectionEnabled = false;
        _projectionDirty = true;
        _viewProjDirty = true;
        _frustumDirty = true;
    }

    private void InvalidateView()
    {
        _viewDirty = true;
        _viewProjDirty = true;
        _frustumDirty = true;
    }
}
