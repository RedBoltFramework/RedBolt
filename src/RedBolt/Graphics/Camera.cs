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

    private float aspectRatio = 1.0f;
    private float farClip = DefaultFarClip;
    private float fov = DefaultFov;
    private float lodBias = 1.0f;
    private float nearClip = DefaultNearClip;
    private float orthoSize = DefaultOrthoSize;
    private float zoom = 1.0f;

    public Camera()
    {
        RegisterAttribute("Near Clip", typeof(float), self => ((Camera)self).GetNearClip(), (self, value) => ((Camera)self).SetNearClip((float)value!), DefaultNearClip);
        RegisterAttribute("Far Clip", typeof(float), self => ((Camera)self).GetFarClip(), (self, value) => ((Camera)self).SetFarClip((float)value!), DefaultFarClip);
        RegisterAttribute("FOV", typeof(float), self => ((Camera)self).GetFov(), (self, value) => ((Camera)self).SetFov((float)value!), DefaultFov);
        RegisterAttribute("Aspect Ratio", typeof(float), self => ((Camera)self).GetAspectRatio(), (self, value) => ((Camera)self).SetAspectRatioInternal((float)value!), 1.0f);
        RegisterAttribute("Fill Mode", typeof(FillMode), self => ((Camera)self).GetFillMode(), (self, value) => ((Camera)self).SetFillMode((FillMode)value!), FillMode.Solid);
        RegisterAttribute("Auto Aspect Ratio", typeof(bool), self => ((Camera)self).GetAutoAspectRatio(), (self, value) => ((Camera)self).SetAutoAspectRatio(value is bool enabled && enabled), true);
        RegisterAttribute("Orthographic", typeof(bool), self => ((Camera)self).IsOrthographic(), (self, value) => ((Camera)self).SetOrthographic(value is bool enabled && enabled), false);
        RegisterAttribute("Orthographic Size", typeof(float), self => ((Camera)self).GetOrthoSize(), (self, value) => ((Camera)self).SetOrthoSize((float)value!), DefaultOrthoSize);
        RegisterAttribute("Zoom", typeof(float), self => ((Camera)self).GetZoom(), (self, value) => ((Camera)self).SetZoom((float)value!), 1.0f);
        RegisterAttribute("LOD Bias", typeof(float), self => ((Camera)self).GetLodBias(), (self, value) => ((Camera)self).SetLodBias((float)value!), 1.0f);
        RegisterAttribute("View Mask", typeof(uint), self => ((Camera)self).GetViewMask(), (self, value) => ((Camera)self).SetViewMask((uint)value!), DefaultViewMask);
        RegisterAttribute("Primary View Mask", typeof(uint), self => ((Camera)self).GetPrimaryViewMask(), (self, value) => ((Camera)self).SetPrimaryViewMask((uint)value!), DefaultViewMask);
        RegisterAttribute("Shadow View Mask", typeof(uint), self => ((Camera)self).GetShadowViewMask(), (self, value) => ((Camera)self).SetShadowViewMask((uint)value!), DefaultViewMask);
        RegisterAttribute("Zone Mask", typeof(uint), self => ((Camera)self).GetZoneMask(), (self, value) => ((Camera)self).SetZoneMask((uint)value!), DefaultZoneMask);
        RegisterAttribute("View Override Flags", typeof(ViewOverride), self => ((Camera)self).GetViewOverrideFlags(), (self, value) => ((Camera)self).SetViewOverrideFlags((ViewOverride)value!), ViewOverride.None);
        RegisterAttribute("Projection Offset", typeof(Vector2), self => ((Camera)self).GetProjectionOffset(), (self, value) => ((Camera)self).SetProjectionOffset((Vector2)value!), Vector2.Zero);
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

    public bool DrawDebugGeometry { get; set; } = true;

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

    public Vector2 MousePosition { get; private set; }

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

    public Vector2 ProjectionOffset { get; set; }

    public uint ShadowViewMask { get; set; } = DefaultViewMask;

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

    public float GetAspectRatio() => aspectRatio;

    public bool GetAutoAspectRatio() => AutoAspectRatio;

    public bool GetDrawDebugGeometry() => DrawDebugGeometry;

    public float GetDistance(Vector3 worldPos)
    {
        if (!Orthographic)
        {
            var cameraPos = IsAttached ? Node.WorldPosition : Vector3.Zero;
            return (worldPos - cameraPos).Length;
        }

        var viewPos = IsAttached ? Node.WorldToLocal(worldPos) : worldPos;
        return Math.Abs(viewPos.Z);
    }

    public float GetDistanceSquared(Vector3 worldPos)
    {
        if (!Orthographic)
        {
            var cameraPos = IsAttached ? Node.WorldPosition : Vector3.Zero;
            return (worldPos - cameraPos).LengthSquared;
        }

        var distance = GetDistance(worldPos);
        return distance * distance;
    }

    public float GetFarClip() => farClip;

    public FillMode GetFillMode() => FillMode;

    public bool GetFlipVertical() => FlipVertical;

    public float GetFov() => fov;

    public float GetHalfViewSize() => !Orthographic
        ? MathF.Tan(fov * MathDefs.DegToRad * 0.5f) / zoom
        : orthoSize * 0.5f / zoom;

    public float GetLodBias() => lodBias;

    public float GetLodDistance(float distance, float scale, float bias)
    {
        var divisor = Math.Max(lodBias * bias * scale * zoom, MathDefs.Epsilon);
        return !Orthographic ? distance / divisor : orthoSize / divisor;
    }

    public float GetNearClip() => nearClip;

    public Matrix3x4 GetView()
    {
        var worldTransform = GetEffectiveWorldTransform();
        return new Matrix3x4(worldTransform.Matrix4.Inverted);
    }

    public Matrix3x4 GetInverseView() => GetEffectiveWorldTransform();

    public Matrix4 GetProjection(bool ignoreFlip = false)
    {
        Matrix4 projection;
        if (!Orthographic)
        {
            var h = (1.0f / MathF.Tan(fov * MathDefs.DegToRad * 0.5f)) * zoom;
            var w = h / Math.Max(aspectRatio, MathDefs.Epsilon);
            var q = farClip / Math.Max(farClip - nearClip, MathDefs.Epsilon);
            var r = -q * nearClip;

            projection = Matrix4.Zero;
            projection.M00 = w;
            projection.M02 = ProjectionOffset.X * 2.0f;
            projection.M11 = h;
            projection.M12 = ProjectionOffset.Y * 2.0f;
            projection.M22 = q;
            projection.M23 = r;
            projection.M32 = 1.0f;
        }
        else
        {
            var h = (1.0f / Math.Max(orthoSize * 0.5f, MathDefs.Epsilon)) * zoom;
            var w = h / Math.Max(aspectRatio, MathDefs.Epsilon);
            var q = 1.0f / Math.Max(farClip, MathDefs.Epsilon);

            projection = Matrix4.Zero;
            projection.M00 = w;
            projection.M03 = ProjectionOffset.X * 2.0f;
            projection.M11 = h;
            projection.M13 = ProjectionOffset.Y * 2.0f;
            projection.M22 = q;
            projection.M33 = 1.0f;
        }

        return FlipVertical && !ignoreFlip ? FlipMatrix * projection : projection;
    }

    public Matrix4 GetGPUProjection()
    {
        var projection = GetProjection();
        projection.M20 = 2.0f * projection.M20 - projection.M30;
        projection.M21 = 2.0f * projection.M21 - projection.M31;
        projection.M22 = 2.0f * projection.M22 - projection.M32;
        projection.M23 = 2.0f * projection.M23 - projection.M33;
        return projection;
    }

    public Matrix4 GetViewProj() => GetProjection() * GetView().Matrix4;

    public Matrix4 GetGPUViewProj() => GetGPUProjection() * GetView().Matrix4;

    public Matrix4 GetInverseViewProj() => GetViewProj().Inverted;

    public float GetOrthoSize() => orthoSize;

    public uint GetPrimaryViewMask() => PrimaryViewMask;

    public Vector2 GetProjectionOffset() => ProjectionOffset;

    public bool GetReverseCulling() => FlipVertical ^ UseReflection;

    public uint GetShadowViewMask() => ShadowViewMask;

    public bool GetUseClipping() => UseClipping;

    public bool GetUseReflection() => UseReflection;

    public Vector2 GetViewSizeAt(float z)
    {
        var halfHeight = GetHalfViewSize();
        var halfSize = new Vector2(aspectRatio * halfHeight, halfHeight);
        return Orthographic ? halfSize : halfSize * z;
    }

    public uint GetViewMask() => PrimaryViewMask | ShadowViewMask;

    public ViewOverride GetViewOverrideFlags() => ViewOverrideFlags;

    public float GetZoom() => zoom;

    public uint GetZoneMask() => ZoneMask;

    public bool HasMousePosition() => MousePosition.X >= 0.0f && MousePosition.X <= 1.0f && MousePosition.Y >= 0.0f && MousePosition.Y <= 1.0f;

    public bool IsOrthographic() => Orthographic;

    public void SetAspectRatio(float value)
    {
        AutoAspectRatio = false;
        SetAspectRatioInternal(value);
    }

    public void SetAspectRatioInternal(float value)
    {
        aspectRatio = value;
    }

    public void SetAutoAspectRatio(bool enable)
    {
        AutoAspectRatio = enable;
    }

    public void SetDrawDebugGeometry(bool enable)
    {
        DrawDebugGeometry = enable;
    }

    public void SetFarClip(float value)
    {
        farClip = Math.Max(value, MathDefs.MinNearClip);
    }

    public void SetFillMode(FillMode mode)
    {
        FillMode = mode;
    }

    public void SetFlipVertical(bool enable)
    {
        FlipVertical = enable;
    }

    public void SetFov(float value)
    {
        fov = MathDefs.Clamp(value, 0.0f, MaxFov);
    }

    public void SetLodBias(float value)
    {
        lodBias = Math.Max(value, MathDefs.Epsilon);
    }

    public void SetMousePosition(Vector2 position)
    {
        MousePosition = position;
    }

    public void SetNearClip(float value)
    {
        nearClip = Math.Max(value, MathDefs.MinNearClip);
    }

    public void SetOrthographic(bool enable)
    {
        Orthographic = enable;
    }

    public void SetOrthoSize(float value)
    {
        orthoSize = value;
        aspectRatio = 1.0f;
    }

    public void SetOrthoSize(Vector2 value)
    {
        AutoAspectRatio = false;
        orthoSize = value.Y;
        aspectRatio = value.Y != 0.0f ? value.X / value.Y : 1.0f;
    }

    public void SetPrimaryViewMask(uint mask)
    {
        PrimaryViewMask = mask;
    }

    public void SetProjectionOffset(Vector2 offset)
    {
        ProjectionOffset = offset;
    }

    public void SetShadowViewMask(uint mask)
    {
        ShadowViewMask = mask;
    }

    public void SetUseClipping(bool enable)
    {
        UseClipping = enable;
    }

    public void SetUseReflection(bool enable)
    {
        UseReflection = enable;
    }

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
        zoom = Math.Max(value, MathDefs.Epsilon);
    }

    public void SetZoneMask(uint mask)
    {
        ZoneMask = mask;
    }

    private Matrix3x4 GetEffectiveWorldTransform()
    {
        return IsAttached
            ? new Matrix3x4(Node.WorldPosition, Node.WorldRotation, 1.0f)
            : Matrix3x4.Identity;
    }
}
