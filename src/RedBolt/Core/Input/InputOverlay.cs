// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Input;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;
using ResourceType = RedBolt.Resource.Resource;

public enum InputOverlayAlignment
{
    TopLeft,
    Top,
    TopRight,
    Left,
    Center,
    Right,
    BottomLeft,
    Bottom,
    BottomRight
}

public sealed class InputOverlayHint
{
    public string Name { get; internal set; } = string.Empty;
    public InputDeviceType DeviceType { get; set; }
    public Texture2D? Texture { get; set; }
    public string Text { get; set; } = string.Empty;
    public Vector2f Anchor { get; set; }
    public Vector2f Offset { get; set; }
    public InputOverlayAlignment Alignment { get; set; }
    public bool Visible { get; set; } = true;
}

public sealed class InputOverlayButton
{
    internal int TouchId = -1;
    internal string Channel = string.Empty;
    public string Name { get; internal set; } = string.Empty;
    public string Action { get; internal set; } = string.Empty;
    public Texture2D? Texture { get; set; }
    public string Text { get; set; } = string.Empty;
    public Vector2f Anchor { get; set; }
    public Vector2f Offset { get; set; }
    public InputOverlayAlignment Alignment { get; set; }
    /// <summary>Control size in pixels at the 96 DPI reference scale.</summary>
    public Vector2f Size { get; set; }
    public bool Visible { get; set; } = true;
}

public sealed class InputOverlayJoystick
{
    internal int TouchId = -1;
    internal Vector2f Origin;
    internal string HorizontalChannel = string.Empty;
    internal string VerticalChannel = string.Empty;
    internal string UpAction = string.Empty;
    internal string DownAction = string.Empty;
    internal string LeftAction = string.Empty;
    internal string RightAction = string.Empty;
    public string Name { get; internal set; } = string.Empty;
    public Vector4f ActivationRegion { get; set; } = new(0.0f, 0.0f, 0.5f, 1.0f);
    /// <summary>Joystick radius in pixels at the 96 DPI reference scale.</summary>
    public float Radius { get; set; } = 54.0f;
    public float DeadZone { get; set; } = 0.025f;
    public bool Visible { get; set; } = true;
    public Vector2f Position { get; internal set; }
}

[Reflected(ObjectCategory.Category_Geometry)]
public partial class InputOverlay : CustomGeometry
{
    private readonly Dictionary<string, InputOverlayHint> _hints = new(StringComparer.Ordinal);
    private readonly Dictionary<string, InputOverlayButton> _buttons = new(StringComparer.Ordinal);
    private readonly Dictionary<string, InputOverlayJoystick> _joysticks = new(StringComparer.Ordinal);
    private readonly Input _input;
    private InputMap? _inputMap;
    private Font? _font;
    private readonly Technique _technique;
    private readonly Material _material;
    private readonly Dictionary<Texture2D, Material> _textureMaterials = [];

    public InputOverlay()
    {
        _input = GetSubsystem<Input>()!;
        _technique = new Technique();
        var pass = _technique.CreatePass("alpha");
        pass.SetVertexShader("Unlit");
        pass.SetPixelShader("Unlit");
        pass.SetVertexShaderDefines("VERTEXCOLOR");
        pass.SetPixelShaderDefines("VERTEXCOLOR");
        pass.SetBlendMode(BlendMode.Alpha);
        pass.SetDepthWrite(false);
        pass.SetDepthTestMode(CompareMode.Always);
        _material = new Material();
        _material.SetTechnique(0, _technique);
        _material.SetCullMode(CullMode.None);
        SetMaterial(_material);
        SetDynamic(true);
        SetCastShadows(false);
        SetOccluder(false);
        SetOccludee(false);
        SubscribeToEvent<InputEvents.TouchBeginEvent>(_input, HandleTouchBegin);
        SubscribeToEvent<InputEvents.TouchMoveEvent>(_input, HandleTouchMove);
        SubscribeToEvent<InputEvents.TouchEndEvent>(_input, HandleTouchEnd);
        SubscribeToEvent<InputEvents.InputDeviceChangedEvent>(_input, HandleInputDeviceChanged);
        SubscribeToEvent<InputEvents.InputFocusEvent>(_input, HandleInputFocus);
        SubscribeToEvent<InputEvents.InputBeginEvent>(_input, HandleInputBegin);
    }

    public InputOverlay(InputMap inputMap)
        : this()
    {
        SetInputMap(inputMap);
    }

    public IReadOnlyCollection<InputOverlayHint> Hints => _hints.Values;
    public IReadOnlyCollection<InputOverlayButton> Buttons => _buttons.Values;
    public IReadOnlyCollection<InputOverlayJoystick> Joysticks => _joysticks.Values;

    [Serialized("Opacity")]
    public float Opacity { get; set; } = 0.8f;

    [Serialized("Control Size")]
    public float ControlSize { get; set; } = 72.0f;

    [Serialized("Font Size")]
    public float FontSize { get; set; } = 18.0f;

    public void SetFont(Font? font) => _font = font;
    public Font? GetFont() => _font;

    /// <summary>Attach the overlay to a scene so it participates in rendering.</summary>
    public void AttachToScene(Scene scene, uint viewMask = DefaultViewMask)
    {
        ArgumentNullException.ThrowIfNull(scene);
        SetViewMask(viewMask);
        if (ReferenceEquals(Scene, scene))
            return;
        Remove();
        scene.CreateTemporaryChild("InputOverlay").AddComponent(this);
    }

    /// <summary>Attach the overlay to the scene rendered by the primary viewport, if one is available.</summary>
    public bool AttachToPrimaryViewport(uint viewMask = DefaultViewMask)
    {
        if (TryGetNode() is not null)
            return true;
        var scene = GetSubsystem<Renderer>()?.GetViewport()?.GetScene();
        if (scene is null)
            return false;
        AttachToScene(scene, viewMask);
        return true;
    }

    public void SetFontAttr(ResourceRef value)
    {
        _font = string.IsNullOrEmpty(value.Name)
            ? null
            : GetSubsystem<ResourceCache>()!.GetResourceOrNull<Font>(value.Name);
    }

    [Serialized("Font", DefaultSource = nameof(GetFontAttributeDefault), Setter = nameof(SetFontAttr))]
    public ResourceRef GetFontAttr() => ResourceType.GetResourceRef(_font, ObjectReflection<Font>.TypeId);

    [Serialized("Control Color")]
    public Color ControlColor { get; set; } = Color.White;

    public void SetInputMap(InputMap? inputMap)
    {
        if (ReferenceEquals(_inputMap, inputMap))
            return;
        if (_inputMap is not null)
        {
            foreach (var button in _buttons.Values)
                _inputMap.UnmapScreenButton(button.Action, button.Channel);
            foreach (var joystick in _joysticks.Values)
                UnmapJoystick(_inputMap, joystick);
        }
        _inputMap = inputMap;
        if (_inputMap is not null)
        {
            foreach (var button in _buttons.Values)
                _inputMap.MapScreenButton(button.Action, button.Channel);
            foreach (var joystick in _joysticks.Values)
                MapJoystick(_inputMap, joystick);
        }
    }
    public InputMap? GetInputMap() => _inputMap;

    public InputOverlayHint AddHint(string name, InputDeviceType deviceType, Texture2D? texture, string text,
        Vector2f anchor, Vector2f offset, InputOverlayAlignment alignment = InputOverlayAlignment.Center)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var hint = new InputOverlayHint
        {
            Name = name, DeviceType = deviceType, Texture = texture, Text = text,
            Anchor = anchor, Offset = offset, Alignment = alignment
        };
        _hints[name] = hint;
        return hint;
    }

    public bool RemoveHint(string name) => _hints.Remove(name);
    public void SetHintVisible(string name, bool visible) => _hints[name].Visible = visible;
    public bool IsHintVisible(string name)
    {
        var hint = _hints[name];
        return hint.Visible && hint.DeviceType == _input.ActiveInputDevice;
    }
    public void UpdateHint(string name, Texture2D? texture, string text)
    {
        var hint = _hints[name];
        hint.Texture = texture;
        hint.Text = text;
    }

    public InputOverlayButton AddButton(string name, string action, Texture2D? texture, string text,
        Vector2f anchor, Vector2f offset, InputOverlayAlignment alignment = InputOverlayAlignment.Center,
        Vector2f? size = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        RemoveButton(name);
        var channel = $"InputOverlay.{name}";
        var button = new InputOverlayButton
        {
            Name = name, Action = action, Channel = channel, Texture = texture, Text = text,
            Anchor = anchor, Offset = offset, Alignment = alignment,
            Size = size ?? new Vector2f(ControlSize, ControlSize)
        };
        _buttons[name] = button;
        UpdateGeometryCount();
        _inputMap?.MapScreenButton(action, channel);
        return button;
    }

    public bool RemoveButton(string name)
    {
        if (!_buttons.Remove(name, out var button))
            return false;
        _input.SetScreenButton(button.Channel, false);
        _inputMap?.UnmapScreenButton(button.Action, button.Channel);
        UpdateGeometryCount();
        return true;
    }

    public void SetButtonVisible(string name, bool visible)
    {
        var button = _buttons[name];
        button.Visible = visible;
        if (!visible)
            Release(button);
    }
    public bool IsButtonVisible(string name) => _buttons[name].Visible && _input.ActiveInputDevice == InputDeviceType.Touch;
    public bool IsButtonDown(string name) => _input.GetScreenButtonDown(_buttons[name].Channel);
    public bool IsButtonPressed(string name) => _input.GetScreenButtonPress(_buttons[name].Channel);
    public void UpdateButton(string name, Texture2D? texture, string text)
    {
        var button = _buttons[name];
        button.Texture = texture;
        button.Text = text;
    }

    public InputOverlayJoystick AddJoystick(string name, string upAction, string downAction,
        string leftAction, string rightAction, Vector4f? activationRegion = null, float radius = 54.0f,
        float deadZone = 0.025f)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        RemoveJoystick(name);
        var joystick = new InputOverlayJoystick
        {
            Name = name,
            HorizontalChannel = $"InputOverlay.{name}.X",
            VerticalChannel = $"InputOverlay.{name}.Y",
            UpAction = upAction, DownAction = downAction, LeftAction = leftAction, RightAction = rightAction,
            ActivationRegion = activationRegion ?? new Vector4f(0.0f, 0.0f, 0.5f, 1.0f),
            Radius = MathF.Max(radius, 1.0f),
            DeadZone = MathDefs.Clamp(deadZone, 0.0f, 1.0f)
        };
        _joysticks[name] = joystick;
        UpdateGeometryCount();
        if (_inputMap is not null)
        {
            MapJoystick(_inputMap, joystick);
        }
        return joystick;
    }

    public bool RemoveJoystick(string name)
    {
        if (!_joysticks.Remove(name, out var joystick))
            return false;
        Release(joystick);
        if (_inputMap is not null)
            UnmapJoystick(_inputMap, joystick);
        UpdateGeometryCount();
        return true;
    }

    public void SetJoystickVisible(string name, bool visible)
    {
        var joystick = _joysticks[name];
        joystick.Visible = visible;
        if (!visible)
            Release(joystick);
    }
    public bool IsJoystickVisible(string name) => _joysticks[name].Visible && _input.ActiveInputDevice == InputDeviceType.Touch;
    public bool IsTouchCaptured(int touchId)
    {
        foreach (var button in _buttons.Values)
        {
            if (button.TouchId == touchId)
                return true;
        }
        foreach (var joystick in _joysticks.Values)
        {
            if (joystick.TouchId == touchId)
                return true;
        }
        return false;
    }
    public void UpdateJoystick(string name, Vector4f activationRegion, float radius, float deadZone)
    {
        var joystick = _joysticks[name];
        joystick.ActivationRegion = activationRegion;
        joystick.Radius = MathF.Max(radius, 1.0f);
        joystick.DeadZone = MathDefs.Clamp(deadZone, 0.0f, 1.0f);
    }

    public override void Dispose()
    {
        ResetControls();
        SetInputMap(null);
        _buttons.Clear();
        _joysticks.Clear();
        _hints.Clear();
        Remove();
        foreach (var material in _textureMaterials.Values)
            material.Dispose();
        _textureMaterials.Clear();
        _material.Dispose();
        _technique.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    public override void UpdateBatches(FrameInfo frame)
    {
        var camera = frame.Camera;
        if (camera is null)
            return;

        var transform = camera.GetEffectiveWorldTransform();
        for (var i = 0; i < _batches.Count; ++i)
        {
            var batch = _batches[i];
            batch.Distance = camera.NearClip;
            _worldTransform[0] = transform;
            batch.WorldTransform = _worldTransform;
            _batches[i] = batch;
        }
    }

    public override void UpdateGeometry(FrameInfo frame) => RebuildGeometry(frame);

    public override UpdateGeometryType GetUpdateGeometryType() => UpdateGeometryType.MainThread;

    protected override void OnWorldBoundingBoxUpdate()
    {
        _worldBoundingBox = new BoundingBox(float.MinValue, float.MaxValue);
        _worldBoundingBoxDirty = false;
    }

    private void HandleTouchBegin(ref InputEvents.TouchBeginEvent value)
    {
        if (_input.ActiveInputDevice != InputDeviceType.Touch)
            return;
        var point = new Vector2f(value.X, value.Y);
        foreach (var button in _buttons.Values)
        {
            if (button.Visible && button.TouchId < 0 && HitTest(button, point))
            {
                button.TouchId = value.TouchID;
                _input.SetTouchCapturedElement(value.TouchID, this);
                _input.SetScreenButton(button.Channel, true);
                return;
            }
        }
        var size = _input.GetBackbufferSize();
        if (size.X <= 0 || size.Y <= 0)
            return;
        var normalized = new Vector2f(point.X / size.X, point.Y / size.Y);
        foreach (var joystick in _joysticks.Values)
        {
            var region = joystick.ActivationRegion;
            if (joystick.Visible && joystick.TouchId < 0 && normalized.X >= region.X && normalized.Y >= region.Y
                && normalized.X <= region.Z && normalized.Y <= region.W)
            {
                joystick.TouchId = value.TouchID;
                _input.SetTouchCapturedElement(value.TouchID, this);
                joystick.Origin = point;
                joystick.Position = point;
                return;
            }
        }
    }

    private void HandleTouchMove(ref InputEvents.TouchMoveEvent value)
    {
        var point = new Vector2f(value.X, value.Y);
        foreach (var button in _buttons.Values)
        {
            if (button.TouchId == value.TouchID)
                _input.SetScreenButton(button.Channel, button.Visible && HitTest(button, point));
        }
        foreach (var joystick in _joysticks.Values)
        {
            if (joystick.TouchId != value.TouchID)
                continue;
            var delta = point - joystick.Origin;
            var length = delta.Length;
            var radius = GetJoystickRadius(joystick);
            if (length > radius)
                delta *= radius / length;
            joystick.Position = joystick.Origin + delta;
            ApplyJoystick(joystick);
        }
    }

    private void HandleTouchEnd(ref InputEvents.TouchEndEvent value)
    {
        foreach (var button in _buttons.Values)
        {
            if (button.TouchId == value.TouchID)
                Release(button);
        }
        foreach (var joystick in _joysticks.Values)
        {
            if (joystick.TouchId == value.TouchID)
                Release(joystick);
        }
    }

    private void HandleInputDeviceChanged(ref InputEvents.InputDeviceChangedEvent value)
    {
        if (value.DeviceType != InputDeviceType.Touch)
            ResetControls();
    }

    private void HandleInputBegin(ref InputEvents.InputBeginEvent value)
    {
        foreach (var joystick in _joysticks.Values)
        {
            if (joystick.TouchId < 0)
                continue;
            ApplyJoystick(joystick);
        }
    }

    private void HandleInputFocus(ref InputEvents.InputFocusEvent value)
    {
        if (!value.Focus)
            ResetControls();
    }

    private bool HitTest(InputOverlayButton button, Vector2f point)
    {
        GetButtonRectangle(button, out var origin, out var size);
        return point.X >= origin.X && point.Y >= origin.Y && point.X <= origin.X + size.X && point.Y <= origin.Y + size.Y;
    }

    private void GetButtonRectangle(InputOverlayButton button, out Vector2f origin, out Vector2f size)
    {
        var scale = GetControlScale();
        size = new Vector2f(button.Size.X * scale.X, button.Size.Y * scale.Y);
        var viewport = _input.GetBackbufferSize();
        origin = new Vector2f(button.Anchor.X * viewport.X + button.Offset.X * scale.X,
            button.Anchor.Y * viewport.Y + button.Offset.Y * scale.Y);
        var alignment = GetAlignment(button.Alignment);
        origin -= new Vector2f(size.X * alignment.X, size.Y * alignment.Y);
    }

    private void RebuildGeometry(FrameInfo frame)
    {
        UpdateGeometryCount();
        for (var i = 0u; i < GetNumGeometries(); ++i)
            BeginGeometry(i, PrimitiveType.TriangleList);
        var geometryIndex = 0u;
        if (_input.ActiveInputDevice == InputDeviceType.Touch && frame.ViewSize.X > 0 && frame.ViewSize.Y > 0)
        {
            var scale = GetControlScale();
            var color = new Color(ControlColor, MathDefs.Clamp(ControlColor.A * Opacity, 0.0f, 1.0f));
            var faint = new Color(color, color.A * 0.75f);
            foreach (var joystick in _joysticks.Values)
            {
                BeginGeometry(geometryIndex, PrimitiveType.TriangleList);
                if (joystick.Visible)
                {
                    var center = joystick.TouchId >= 0
                        ? joystick.Origin
                        : new Vector2f(frame.ViewSize.X * 0.25f, frame.ViewSize.Y * 0.75f);
                    var radius = joystick.Radius * (scale.X + scale.Y) * 0.5f;
                    var knob = joystick.TouchId >= 0 ? joystick.Position : center;
                    AddRing(frame, center, radius, MathF.Max(3.0f, radius * 0.08f), faint);
                    AddCircle(frame, knob, radius * 0.38f, color);
                }
                SetMaterial((int)geometryIndex, _material);
                ++geometryIndex;
            }
            foreach (var button in _buttons.Values)
            {
                BeginGeometry(geometryIndex, PrimitiveType.TriangleList);
                if (button.Visible)
                {
                    GetButtonRectangle(button, out var origin, out var size);
                    var center = origin + size * 0.5f;
                    if (button.Texture is not null)
                    {
                        AddTexturedQuad(frame, center, size, button.TouchId >= 0 ? color : faint);
                        SetMaterial((int)geometryIndex, GetTextureMaterial(button.Texture));
                    }
                    else
                    {
                        var radius = MathF.Min(size.X, size.Y) * 0.5f;
                        AddCircle(frame, center, radius, button.TouchId >= 0 ? color : faint);
                        AddRing(frame, center, radius, MathF.Max(3.0f, radius * 0.1f), color);
                        SetMaterial((int)geometryIndex, _material);
                    }
                }
                ++geometryIndex;
            }
        }
        Commit();
        if (geometryIndex == 0)
            SetMaterial(0, _material);
    }

    private void UpdateGeometryCount() =>
        SetNumGeometries((uint)System.Math.Max(_buttons.Count + _joysticks.Count, 1));

    private Material GetTextureMaterial(Texture2D texture)
    {
        if (_textureMaterials.TryGetValue(texture, out var material))
            return material;
        material = new Material();
        material.SetTechnique(0, _technique);
        material.SetTexture("Albedo", texture);
        material.SetCullMode(CullMode.None);
        _textureMaterials.Add(texture, material);
        return material;
    }

    private void AddTexturedQuad(FrameInfo frame, Vector2f center, Vector2f size, Color color)
    {
        var halfSize = size * 0.5f;
        var topLeft = center - halfSize;
        var bottomRight = center + halfSize;
        AddTexturedVertex(frame, topLeft, new Vector2f(0.0f, 0.0f), color);
        AddTexturedVertex(frame, new Vector2f(bottomRight.X, topLeft.Y), new Vector2f(1.0f, 0.0f), color);
        AddTexturedVertex(frame, bottomRight, new Vector2f(1.0f, 1.0f), color);
        AddTexturedVertex(frame, topLeft, new Vector2f(0.0f, 0.0f), color);
        AddTexturedVertex(frame, bottomRight, new Vector2f(1.0f, 1.0f), color);
        AddTexturedVertex(frame, new Vector2f(topLeft.X, bottomRight.Y), new Vector2f(0.0f, 1.0f), color);
    }

    private void AddTexturedVertex(FrameInfo frame, Vector2f position, Vector2f texCoord, Color color)
    {
        AddVertex(frame, position, color);
        DefineTexCoord(texCoord);
    }

    private void AddRing(FrameInfo frame, Vector2f center, float radius, float width, Color color)
    {
        const int segments = 32;
        var inner = MathF.Max(0.0f, radius - width);
        for (var i = 0; i < segments; ++i)
        {
            var a0 = i * (MathF.PI * 2.0f / segments);
            var a1 = (i + 1) * (MathF.PI * 2.0f / segments);
            var outer0 = center + new Vector2f(MathF.Cos(a0), MathF.Sin(a0)) * radius;
            var outer1 = center + new Vector2f(MathF.Cos(a1), MathF.Sin(a1)) * radius;
            var inner0 = center + new Vector2f(MathF.Cos(a0), MathF.Sin(a0)) * inner;
            var inner1 = center + new Vector2f(MathF.Cos(a1), MathF.Sin(a1)) * inner;
            AddTriangle(frame, outer0, outer1, inner1, color);
            AddTriangle(frame, outer0, inner1, inner0, color);
        }
    }

    private void AddCircle(FrameInfo frame, Vector2f center, float radius, Color color)
    {
        const int segments = 32;
        for (var i = 0; i < segments; ++i)
        {
            var a0 = i * (MathF.PI * 2.0f / segments);
            var a1 = (i + 1) * (MathF.PI * 2.0f / segments);
            AddTriangle(frame, center,
                center + new Vector2f(MathF.Cos(a0), MathF.Sin(a0)) * radius,
                center + new Vector2f(MathF.Cos(a1), MathF.Sin(a1)) * radius, color);
        }
    }

    private void AddLine(FrameInfo frame, Vector2f start, Vector2f end, float width, Color color)
    {
        var direction = end - start;
        if (direction.LengthSquared <= MathDefs.Epsilon)
            return;
        direction.Normalize();
        var offset = new Vector2f(-direction.Y, direction.X) * (width * 0.5f);
        AddTriangle(frame, start - offset, end - offset, end + offset, color);
        AddTriangle(frame, start - offset, end + offset, start + offset, color);
    }

    private void AddTriangle(FrameInfo frame, Vector2f a, Vector2f b, Vector2f c, Color color)
    {
        AddVertex(frame, a, color);
        AddVertex(frame, b, color);
        AddVertex(frame, c, color);
    }

    private void AddVertex(FrameInfo frame, Vector2f position, Color color)
    {
        var camera = frame.Camera!;
        var distance = MathF.Max(camera.NearClip * 1.1f, 0.01f);
        var halfHeight = camera.GetHalfViewSize() * (camera.Orthographic ? 1.0f : distance);
        var halfWidth = halfHeight * frame.ViewSize.X / frame.ViewSize.Y;
        DefineVertex(new Vector3f(
            (position.X / frame.ViewSize.X * 2.0f - 1.0f) * halfWidth,
            (1.0f - position.Y / frame.ViewSize.Y * 2.0f) * halfHeight,
            distance));
        DefineColor(color);
    }

    private static Vector2f GetAlignment(InputOverlayAlignment alignment) => alignment switch
    {
        InputOverlayAlignment.TopLeft => new Vector2f(0.0f, 0.0f),
        InputOverlayAlignment.Top => new Vector2f(0.5f, 0.0f),
        InputOverlayAlignment.TopRight => new Vector2f(1.0f, 0.0f),
        InputOverlayAlignment.Left => new Vector2f(0.0f, 0.5f),
        InputOverlayAlignment.Center => new Vector2f(0.5f, 0.5f),
        InputOverlayAlignment.Right => new Vector2f(1.0f, 0.5f),
        InputOverlayAlignment.BottomLeft => new Vector2f(0.0f, 1.0f),
        InputOverlayAlignment.Bottom => new Vector2f(0.5f, 1.0f),
        _ => new Vector2f(1.0f, 1.0f)
    };

    private void ResetControls()
    {
        foreach (var button in _buttons.Values)
            Release(button);
        foreach (var joystick in _joysticks.Values)
            Release(joystick);
    }

    private void Release(InputOverlayButton button)
    {
        button.TouchId = -1;
        _input.SetScreenButton(button.Channel, false);
    }

    private void Release(InputOverlayJoystick joystick)
    {
        joystick.TouchId = -1;
        joystick.Position = joystick.Origin;
        _input.SetScreenAxis(joystick.HorizontalChannel, 0.0f);
        _input.SetScreenAxis(joystick.VerticalChannel, 0.0f);
    }

    private void ApplyJoystick(InputOverlayJoystick joystick)
    {
        var value = (joystick.Position - joystick.Origin) / GetJoystickRadius(joystick);
        if (value.Length <= joystick.DeadZone)
            value = Vector2f.Zero;
        _input.SetScreenAxis(joystick.HorizontalChannel, value.X);
        _input.SetScreenAxis(joystick.VerticalChannel, value.Y);
    }

    private Vector2f GetControlScale()
    {
        const float referenceDpi = 96.0f;
        var fallback = _input.SystemToBackbufferScale;
        var dpi = GetSubsystem<Graphics>()?.GetDisplayDPI() ?? Vector3f.Zero;
        return new Vector2f(dpi.X > 0.0f ? dpi.X / referenceDpi : fallback.X,
            dpi.Y > 0.0f ? dpi.Y / referenceDpi : fallback.Y);
    }

    private float GetJoystickRadius(InputOverlayJoystick joystick)
    {
        var scale = GetControlScale();
        return joystick.Radius * (scale.X + scale.Y) * 0.5f;
    }

    private static void MapJoystick(InputMap inputMap, InputOverlayJoystick joystick)
    {
        inputMap.MapScreenAxis(joystick.LeftAction, joystick.HorizontalChannel, 0.0f, -1.0f);
        inputMap.MapScreenAxis(joystick.RightAction, joystick.HorizontalChannel, 0.0f, 1.0f);
        inputMap.MapScreenAxis(joystick.UpAction, joystick.VerticalChannel, 0.0f, -1.0f);
        inputMap.MapScreenAxis(joystick.DownAction, joystick.VerticalChannel, 0.0f, 1.0f);
    }

    private static void UnmapJoystick(InputMap inputMap, InputOverlayJoystick joystick)
    {
        inputMap.UnmapScreenAxis(joystick.LeftAction, joystick.HorizontalChannel);
        inputMap.UnmapScreenAxis(joystick.RightAction, joystick.HorizontalChannel);
        inputMap.UnmapScreenAxis(joystick.UpAction, joystick.VerticalChannel);
        inputMap.UnmapScreenAxis(joystick.DownAction, joystick.VerticalChannel);
    }

    private static object? GetFontAttributeDefault() => new ResourceRef(ObjectReflection<Font>.TypeId);
}
