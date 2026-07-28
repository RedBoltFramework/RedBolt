using SystemMath = global::System.Math;

using RedBolt.Core;
using RedBolt.Math;
using RedBolt.RenderAPI;
using RedBolt.Scene;

namespace RedBolt.Graphics;

[Flags]
public enum ViewOverride : uint
{
    None = 0,
    LowMaterialQuality = 1 << 0,
    DisableShadows = 1 << 1,
    DisableOcclusion = 1 << 2
}

[Reflected(ObjectCategory.Category_Scene)]
[Serialized(
    "Is Enabled",
    typeof(bool),
    DefaultSource = nameof(GetIsEnabledAttributeDefault),
    Getter = nameof(Enabled),
    Setter = nameof(SetEnabled)
)]
[Serialized(
    "Near Clip",
    typeof(float),
    DefaultSource = nameof(GetNearClipAttributeDefault),
    Getter = nameof(GetNearClip),
    Setter = nameof(SetNearClip)
)]
[Serialized(
    "Far Clip",
    typeof(float),
    DefaultSource = nameof(GetFarClipAttributeDefault),
    Getter = nameof(GetFarClip),
    Setter = nameof(SetFarClip)
)]
[Serialized(
    "FOV",
    typeof(float),
    DefaultSource = nameof(GetFOVAttributeDefault),
    Getter = nameof(GetFov),
    Setter = nameof(SetFov)
)]
[Serialized(
    "Aspect Ratio",
    typeof(float),
    DefaultSource = nameof(GetAspectRatioAttributeDefault),
    Getter = nameof(GetAspectRatio),
    Setter = nameof(SetAspectRatioInternal)
)]
[Serialized(
    "Fill Mode",
    typeof(FillMode),
    DefaultSource = nameof(GetFillModeAttributeDefault),
    Getter = nameof(GetFillMode),
    Setter = nameof(SetFillMode)
)]
[Serialized(
    "Auto Aspect Ratio",
    typeof(bool),
    DefaultSource = nameof(GetAutoAspectRatioAttributeDefault),
    Getter = nameof(GetAutoAspectRatio),
    Setter = nameof(SetAutoAspectRatio)
)]
[Serialized(
    "Orthographic",
    typeof(bool),
    DefaultSource = nameof(GetOrthographicAttributeDefault),
    Getter = nameof(IsOrthographic),
    Setter = nameof(SetOrthographic)
)]
[Serialized(
    "Orthographic Size",
    typeof(float),
    DefaultSource = nameof(GetOrthographicSizeAttributeDefault),
    Getter = nameof(GetOrthoSize),
    Setter = nameof(SetOrthoSizeAttr)
)]
[Serialized(
    "Zoom",
    typeof(float),
    DefaultSource = nameof(GetZoomAttributeDefault),
    Getter = nameof(GetZoom),
    Setter = nameof(SetZoom)
)]
[Serialized(
    "LOD Bias",
    typeof(float),
    DefaultSource = nameof(GetLODBiasAttributeDefault),
    Getter = nameof(GetLodBias),
    Setter = nameof(SetLodBias)
)]
[Serialized(
    "View Mask",
    typeof(uint),
    DefaultSource = nameof(GetViewMaskAttributeDefault),
    Getter = nameof(GetViewMask),
    Setter = nameof(SetViewMask)
)]
[Serialized(
    "Primary View Mask",
    typeof(uint),
    DefaultSource = nameof(GetPrimaryViewMaskAttributeDefault),
    Getter = nameof(GetPrimaryViewMask),
    Setter = nameof(SetPrimaryViewMask)
)]
[Serialized(
    "Shadow View Mask",
    typeof(uint),
    DefaultSource = nameof(GetShadowViewMaskAttributeDefault),
    Getter = nameof(GetShadowViewMask),
    Setter = nameof(SetShadowViewMask)
)]
[Serialized(
    "Zone Mask",
    typeof(uint),
    DefaultSource = nameof(GetZoneMaskAttributeDefault),
    Getter = nameof(GetZoneMask),
    Setter = nameof(SetZoneMask)
)]
[Serialized(
    "View Override Flags",
    typeof(ViewOverride),
    DefaultSource = nameof(GetViewOverrideFlagsAttributeDefault),
    Getter = nameof(GetViewOverrideFlags),
    Setter = nameof(SetViewOverrideFlags)
)]
[Serialized(
    "Projection Offset",
    typeof(Vector2f),
    DefaultSource = nameof(GetProjectionOffsetAttributeDefault),
    Getter = nameof(GetProjectionOffset),
    Setter = nameof(SetProjectionOffset)
)]
[Serialized(
    "Reflection Plane",
    typeof(Vector4f),
    DefaultSource = nameof(GetReflectionPlaneAttributeDefault),
    Getter = nameof(GetReflectionPlaneAttr),
    Setter = nameof(SetReflectionPlaneAttr)
)]
[Serialized(
    "Clip Plane",
    typeof(Vector4f),
    DefaultSource = nameof(GetClipPlaneAttributeDefault),
    Getter = nameof(GetClipPlaneAttr),
    Setter = nameof(SetClipPlaneAttr)
)]
[Serialized(
    "Use Reflection",
    typeof(bool),
    DefaultSource = nameof(GetUseReflectionAttributeDefault),
    Getter = nameof(GetUseReflection),
    Setter = nameof(SetUseReflection)
)]
[Serialized(
    "Use Clipping",
    typeof(bool),
    DefaultSource = nameof(GetUseClippingAttributeDefault),
    Getter = nameof(GetUseClipping),
    Setter = nameof(SetUseClipping)
)]
[Serialized(
    "Draw Debug Geometry",
    typeof(bool),
    DefaultSource = nameof(GetDrawDebugGeometryAttributeDefault),
    Getter = nameof(GetDrawDebugGeometry),
    Setter = nameof(SetDrawDebugGeometry)
)]
public sealed partial class Camera : Component
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
    private readonly Frustum _viewSpaceFrustum = new();
    private readonly WeakReference<GraphicsSystem>? _graphics;
    private Zone? _zone;

    public Camera()
    {
        var graphics = GetSubsystem<GraphicsSystem>();
        _graphics = graphics is not null ? new WeakReference<GraphicsSystem>(graphics) : null;
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
        var result = new Frustum();
        GetSplitFrustum(nearClip, farClip, result);
        return result;
    }

    internal void GetSplitFrustum(float nearClip, float farClip, Frustum result)
    {
        if (_projectionDirty)
            UpdateProjection();

        nearClip = SystemMath.Max(nearClip, _projectionNearClip);
        farClip = SystemMath.Min(farClip, _projectionFarClip);
        if (farClip < nearClip)
            farClip = nearClip;

        if (_customProjectionEnabled)
        {
            result.DefineSplit(GetProjection(ignoreFlip: true), nearClip, farClip);
            result.Transform(GetEffectiveWorldTransform());
        }
        else if (!Orthographic)
            result.Define(Fov, AspectRatio, Zoom, nearClip, farClip, GetEffectiveWorldTransform());
        else
            result.DefineOrtho(OrthoSize, AspectRatio, Zoom, nearClip, farClip, GetEffectiveWorldTransform());
    }

    public Frustum GetViewSpaceFrustum()
    {
        var result = new Frustum();
        GetViewSpaceFrustum(result);
        return result;
    }

    internal void GetViewSpaceFrustum(Frustum result)
    {
        if (_projectionDirty)
            UpdateProjection();

        if (_customProjectionEnabled)
            result.Define(GetProjection(ignoreFlip: true));
        else if (!Orthographic)
            result.Define(Fov, AspectRatio, Zoom, GetNearClip(), GetFarClip());
        else
            result.DefineOrtho(OrthoSize, AspectRatio, Zoom, GetNearClip(), GetFarClip());
    }

    public Frustum GetViewSpaceSplitFrustum(float nearClip, float farClip)
    {
        if (_projectionDirty)
            UpdateProjection();

        nearClip = SystemMath.Max(nearClip, _projectionNearClip);
        farClip = SystemMath.Min(farClip, _projectionFarClip);
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
        GetViewSpaceFrustum(_viewSpaceFrustum);
        nearSize = _viewSpaceFrustum.Vertices[0];
        farSize = _viewSpaceFrustum.Vertices[4];

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
            var cameraPos = TryGetNode() is not null ? Node.WorldPosition : Vector3f.Zero;
            return (worldPos - cameraPos).Length;
        }

        var viewPos = GetView().Matrix4 * new Vector4f(worldPos, 1.0f);
        return SystemMath.Abs(viewPos.Z);
    }

    public float GetDistanceSquared(Vector3f worldPos)
    {
        if (!Orthographic)
        {
            var cameraPos = TryGetNode() is not null ? Node.WorldPosition : Vector3f.Zero;
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
        var divisor = SystemMath.Max(_lodBias * bias * scale * _zoom, MathDefs.Epsilon);
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
                Origin = TryGetNode() is not null ? Node.WorldPosition : Vector3f.Zero,
                Direction = TryGetNode() is not null ? Node.WorldDirection : Vector3f.Forward
            };
            return result;
        }

        var viewProjInverse = (GetProjection() * GetView().Matrix4).Inverse;
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
        var rayDistance = SystemMath.Max(screenPos.Z - GetNearClip(), 0.0f) / viewSpaceDirection.Z;
        return ray.Origin + ray.Direction * rayDistance;
    }

    public float GetOrthoSize() => _orthoSize;

    public uint GetPrimaryViewMask() => PrimaryViewMask;

    public Vector2f GetProjectionOffset() => ProjectionOffset;

    public bool GetReverseCulling() => FlipVertical ^ UseReflection;

    public Plane GetReflectionPlane() => _reflectionPlane;

    public Plane GetClipPlane() => _clipPlane;

    public Zone? GetZone() => _zone;

    public Color GetEffectiveAmbientColor() => GetZone()?.GetAmbientColor() ?? Color.TransparentBlack;

    public float GetEffectiveAmbientBrightness() => GetZone()?.GetAmbientBrightness() ?? 1.0f;

    public Color GetEffectiveFogColor() => GetZone()?.GetFogColor() ?? Color.TransparentBlack;

    public float GetEffectiveFogStart() => GetZone()?.GetFogStart() ?? MathDefs.LargeValue;

    public float GetEffectiveFogEnd() => GetZone()?.GetFogEnd() ?? MathDefs.LargeValue;

    public Quaternion GetFaceCameraRotation(
        Vector3f position, Quaternion rotation, FaceCameraMode mode, float minAngle = 0.0f)
    {
        if (TryGetNode() is null)
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
                var lookAt = new Quaternion();
                lookAt.FromLookRotation(position - Node.WorldPosition, Vector3f.Up);
                return lookAt;
            }

            case FaceCameraMode.LookAtY:
            case FaceCameraMode.LookAtMixed:
            {
                var lookAtVector = position - Node.WorldPosition;
                var lookAtVectorXz = new Vector3f(lookAtVector.X, 0.0f, lookAtVector.Z);
                var lookAt = new Quaternion();
                lookAt.FromLookRotation(lookAtVectorXz, Vector3f.Up);

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
        _farClip = SystemMath.Max(value, MathDefs.MinNearClip);
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
        _lodBias = SystemMath.Max(value, MathDefs.Epsilon);
    }

    public void SetMousePosition(Vector2f position)
    {
        MousePosition = position;
    }

    public void SetNearClip(float value)
    {
        _nearClip = SystemMath.Max(value, MathDefs.MinNearClip);
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
        _reflectionMatrix = plane.ReflectionMatrix;
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

    public Vector4f GetReflectionPlaneAttr() => _reflectionPlane.ToVector4();

    public void SetClipPlaneAttr(Vector4f value)
    {
        SetClipPlane(new Plane(value));
    }

    public Vector4f GetClipPlaneAttr() => _clipPlane.ToVector4();

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
        _zoom = SystemMath.Max(value, MathDefs.Epsilon);
        InvalidateProjection();
    }

    public void SetZoneMask(uint mask)
    {
        ZoneMask = mask;
    }

    public void SetZone(Zone? zone)
    {
        _zone = zone;
    }

    public void SetProjection(Matrix4 projection)
    {
        var projectionInverse = projection.Inverse;
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
        var worldTransform = TryGetNode() is not null
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
        _inverseProjection = projection.Inverse;
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
        _inverseViewProj = _viewProj.Inverse;
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

    private static object? GetIsEnabledAttributeDefault() => true;

    private static object? GetNearClipAttributeDefault() => DefaultNearClip;

    private static object? GetFarClipAttributeDefault() => DefaultFarClip;

    private static object? GetFOVAttributeDefault() => DefaultFov;

    private static object? GetAspectRatioAttributeDefault() => 1.0f;

    private static object? GetFillModeAttributeDefault() => FillMode.Solid;

    private static object? GetAutoAspectRatioAttributeDefault() => true;

    private static object? GetOrthographicAttributeDefault() => false;

    private static object? GetOrthographicSizeAttributeDefault() => DefaultOrthoSize;

    private static object? GetZoomAttributeDefault() => 1.0f;

    private static object? GetLODBiasAttributeDefault() => 1.0f;

    private static object? GetViewMaskAttributeDefault() => DefaultViewMask;

    private static object? GetPrimaryViewMaskAttributeDefault() => DefaultViewMask;

    private static object? GetShadowViewMaskAttributeDefault() => DefaultViewMask;

    private static object? GetZoneMaskAttributeDefault() => DefaultZoneMask;

    private static object? GetViewOverrideFlagsAttributeDefault() => ViewOverride.None;

    private static object? GetProjectionOffsetAttributeDefault() => Vector2f.Zero;

    private static object? GetReflectionPlaneAttributeDefault() => Plane.Up.ToVector4();

    private static object? GetClipPlaneAttributeDefault() => Plane.Up.ToVector4();

    private static object? GetUseReflectionAttributeDefault() => false;

    private static object? GetUseClippingAttributeDefault() => false;

    private static object? GetDrawDebugGeometryAttributeDefault() => true;
}
