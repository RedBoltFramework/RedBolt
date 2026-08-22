// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Editor.Foundation;

using System;

using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
using RedBolt.Editor.Core;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.Scene;
using RedBolt.SystemUI;
using RedBolt.Utility;

/// <summary>Add-on to manage scene selection with mouse and render debug geometry.</summary>
public class TransformManipulator : SceneViewAddon
{
    private static readonly EditorHotkey HotkeyToggleLocal =
        new EditorHotkey("TransformGizmo.ToggleLocal").Press(Key.X);
    private static readonly EditorHotkey HotkeyTogglePivoted =
        new EditorHotkey("TransformGizmo.TogglePivoted").Press(Key.Z);
    private static readonly EditorHotkey HotkeySelect =
        new EditorHotkey("TransformGizmo.Select").Press(Key.Q);
    private static readonly EditorHotkey HotkeyTranslate =
        new EditorHotkey("TransformGizmo.Translate").Press(Key.W);
    private static readonly EditorHotkey HotkeyRotate =
        new EditorHotkey("TransformGizmo.Rotate").Press(Key.E);
    private static readonly EditorHotkey HotkeyScale =
        new EditorHotkey("TransformGizmo.Scale").Press(Key.R);

    public struct Settings : ISimpleSettingsPageValues
    {
        public Settings()
        {
            SnapPosition = 0.5f * Vector3f.One;
            SnapRotation = 5.0f;
            SnapScale = 0.1f;
            ScreenRotation = false;
        }

        public string UniqueName => "Editor.Scene:TransformGizmo";

        public void SerializeInBlock(Archive archive)
        {
            ArchiveSerialization.SerializeOptionalValue(
                archive, "SnapPosition", ref SnapPosition, new Settings().SnapPosition,
                ArchiveSerialization.SerializeValue);
            ArchiveSerialization.SerializeOptionalValue(
                archive, "SnapRotation", ref SnapRotation, new Settings().SnapRotation,
                ArchiveSerialization.SerializeValue);
            ArchiveSerialization.SerializeOptionalValue(
                archive, "SnapScale", ref SnapScale, new Settings().SnapScale,
                ArchiveSerialization.SerializeValue);
            ArchiveSerialization.SerializeOptionalValue(
                archive, "ScreenRotation", ref ScreenRotation, new Settings().ScreenRotation,
                ArchiveSerialization.SerializeValue);
        }

        public void RenderSettings()
        {
            if (ImGui.DragFloat("Snap Position"u8, ref SnapPosition.X, 0.1f, 0.1f, 10.0f, "%.2f"u8))
            {
                SnapPosition.Y = SnapPosition.X;
                SnapPosition.Z = SnapPosition.X;
            }
            ImGui.Indent();
            ImGui.DragFloat("X"u8, ref SnapPosition.X, 0.1f, 0.1f, 10.0f, "%.2f"u8);
            ImGui.DragFloat("Y"u8, ref SnapPosition.Y, 0.1f, 0.1f, 10.0f, "%.2f"u8);
            ImGui.DragFloat("Z"u8, ref SnapPosition.Z, 0.1f, 0.1f, 10.0f, "%.2f"u8);
            ImGui.Unindent();

            ImGui.DragFloat("Snap Rotation"u8, ref SnapRotation, 5.0f, 5.0f, 180.0f, "%.1f"u8);
            ImGui.DragFloat("Snap Scale"u8, ref SnapScale, 0.1f, 0.1f, 1.0f, "%.2f"u8);

            ImGui.Checkbox("Screen Space Rotation in 3D View"u8, ref ScreenRotation);
        }

        public readonly Vector3f GetSnapValue(TransformGizmoOperation operation)
        {
            return operation switch
            {
                TransformGizmoOperation.Translate => SnapPosition,
                TransformGizmoOperation.Rotate => Vector3f.One * SnapRotation,
                TransformGizmoOperation.Scale => Vector3f.One * SnapScale,
                _ => Vector3f.Zero,
            };
        }

        public Vector3f SnapPosition;
        public float SnapRotation;
        public float SnapScale;
        public bool ScreenRotation;
    }

    public class SettingsPage : SimpleSettingsPage<Settings>
    {
    }

    public TransformManipulator(SceneViewTab owner, SettingsPage settings)
        : base(owner)
    {
        _settings = new WeakReference<SettingsPage>(settings);

        var hotkeyManager = owner.GetHotkeyManager();
        hotkeyManager.BindHotkey(this, HotkeyToggleLocal, static self => self.ToggleSpace());
        hotkeyManager.BindHotkey(this, HotkeyTogglePivoted, static self => self.TogglePivoted());
        hotkeyManager.BindHotkey(this, HotkeySelect, static self => self.SetSelect());
        hotkeyManager.BindHotkey(this, HotkeyTranslate, static self => self.SetTranslate());
        hotkeyManager.BindHotkey(this, HotkeyRotate, static self => self.SetRotate());
        hotkeyManager.BindHotkey(this, HotkeyScale, static self => self.SetScale());
    }

    public override void Dispose()
    {
        _transformNodesGizmo?.Dispose();
        _transformNodesGizmo = null;
        _selectionScene?.SetTarget(null!);
        _selectionScene = null;
        _settings.SetTarget(null!);
        _hotkeyLabel.Dispose();
        base.Dispose();
    }

    /// <summary>Commands.</summary>
    public void ToggleSpace() => _isLocal = !_isLocal;

    public void TogglePivoted() => _isPivoted = !_isPivoted;

    public void SetSelect() => _operation = TransformGizmoOperation.None;

    public void SetTranslate() => _operation = TransformGizmoOperation.Translate;

    public void SetRotate() => _operation = TransformGizmoOperation.Rotate;

    public void SetScale() => _operation = TransformGizmoOperation.Scale;

    /// <summary>Getters.</summary>
    public bool IsLocal() => _isLocal;

    public bool IsPivoted() => _isPivoted;

    public bool IsSelect() => _operation == TransformGizmoOperation.None;

    public bool IsTranslate() => _operation == TransformGizmoOperation.Translate;

    public bool IsRotate() => _operation == TransformGizmoOperation.Rotate;

    public bool IsScale() => _operation == TransformGizmoOperation.Scale;

    /// <summary>Implement SceneViewAddon.</summary>
    public override string GetUniqueName() => "TransformGizmo";

    public override int GetToolbarPriority() => 0;

    public override void ProcessInput(SceneViewPage scenePage, ref bool mouseConsumed)
    {
        if (!_settings.TryGetTarget(out var settings))
            return;
        ref readonly var config = ref settings.Values;

        var nodes = scenePage.Selection.EffectiveNodes;
        if (nodes.Count == 0)
            return;

        EnsureGizmoInitialized(scenePage);

        if (!mouseConsumed)
        {
            var camera = scenePage.Renderer.Camera!;
            var gizmo = new TransformGizmo(camera, scenePage.ContentArea);

            var needSnap = IsKeyDown(Key.Ctrl);
            var snapValue = needSnap ? config.GetSnapValue(_operation) : Vector3f.Zero;
            if (_transformNodesGizmo!.Manipulate(
                    gizmo, _operation, GetCurrentAxes(), _isLocal, _isPivoted, snapValue))
            {
                mouseConsumed = true;
            }
        }
    }

    private TransformGizmoAxis GetCurrentAxes()
    {
        const TransformGizmoAxis xyz =
            TransformGizmoAxis.X | TransformGizmoAxis.Y | TransformGizmoAxis.Z;
        if (_operation == TransformGizmoOperation.Rotate
            && _settings.TryGetTarget(out var settings)
            && settings.Values.ScreenRotation)
        {
            return xyz | TransformGizmoAxis.Screen;
        }
        return xyz;
    }

    private void EnsureGizmoInitialized(SceneViewPage scenePage)
    {
        var selectionScene = _selectionScene is not null && _selectionScene.TryGetTarget(out var scene)
            ? scene
            : null;
        if (scenePage.Selection.Revision != _selectionRevision
            || !ReferenceEquals(scenePage.Scene, selectionScene))
        {
            _selectionRevision = scenePage.Selection.Revision;
            _selectionScene = new WeakReference<Scene>(scenePage.Scene);
            _transformNodesGizmo?.Dispose();
            _transformNodesGizmo = null;
        }

        if (_transformNodesGizmo is null)
        {
            var nodes = scenePage.Selection.EffectiveNodes;
            var activeNode = scenePage.Selection.ActiveNode;
            _transformNodesGizmo = new TransformNodesGizmo(activeNode, nodes);
            _transformNodesGizmo.OnNodeTransformChanged += OnNodeTransformChanged;
        }
    }

    private void OnNodeTransformChanged(Node node, Transform oldTransform)
    {
        if (_owner.TryGetTarget(out var owner))
            owner.PushAction(new ChangeNodeTransformAction(node, oldTransform));
    }

    public override void Render(SceneViewPage scenePage)
    {
    }

    public override void ApplyHotkeys(HotkeyManager hotkeyManager)
    {
        hotkeyManager.InvokeFor(this);
    }

    public override bool RenderTabContextMenu()
    {
        if (!_owner.TryGetTarget(out var owner))
            return true;
        var hotkeyManager = owner.GetHotkeyManager();

        if (!ImGui.BeginMenu("Transform Gizmo"u8))
            return true;

        SetHotkeyLabel(hotkeyManager, HotkeyToggleLocal);
        if (ImGui.MenuItem("In Local Space"u8, _hotkeyLabel.ContentBytes, _isLocal))
            _isLocal = !_isLocal;

        SetHotkeyLabel(hotkeyManager, HotkeyTogglePivoted);
        if (ImGui.MenuItem("Is Pivoted"u8, _hotkeyLabel.ContentBytes, _isPivoted))
            _isPivoted = !_isPivoted;

        ImGui.Separator();

        SetHotkeyLabel(hotkeyManager, HotkeySelect);
        if (ImGui.MenuItem("Select"u8, _hotkeyLabel.ContentBytes, IsSelect()))
            _operation = TransformGizmoOperation.None;
        SetHotkeyLabel(hotkeyManager, HotkeyTranslate);
        if (ImGui.MenuItem("Translate"u8, _hotkeyLabel.ContentBytes, IsTranslate()))
            _operation = TransformGizmoOperation.Translate;
        SetHotkeyLabel(hotkeyManager, HotkeyRotate);
        if (ImGui.MenuItem("Rotate"u8, _hotkeyLabel.ContentBytes, IsRotate()))
            _operation = TransformGizmoOperation.Rotate;
        SetHotkeyLabel(hotkeyManager, HotkeyScale);
        if (ImGui.MenuItem("Scale"u8, _hotkeyLabel.ContentBytes, IsScale()))
            _operation = TransformGizmoOperation.Scale;

        ImGui.EndMenu();
        return true;
    }

    private static readonly CString _selectObjectsLabel = new($"{Icons.ArrowPointer}##SelectObjects");
    private static readonly CString _moveObjectsLabel = new($"{Icons.ArrowsUpDownLeftRight}##MoveObjects");
    private static readonly CString _rotateObjectsLabel = new($"{Icons.ArrowsRotate}##RotateObjects");
    private static readonly CString _scaleObjectsLabel = new($"{Icons.ArrowsLeftRightToLine}##ScaleObjects");
    private static readonly CString _transformSpaceLabel = new($"{Icons.Cube}##TransformSpace");
    private static readonly CString _transformPivotLabel = new($"{Icons.ArrowsToDot}##TransformPivot");

    public override bool RenderToolbar()
    {
        if (Widgets.ToolbarButton(_selectObjectsLabel, "Select Objects"u8, IsSelect()))
            _operation = TransformGizmoOperation.None;
        if (Widgets.ToolbarButton(_moveObjectsLabel, "Move Objects"u8, IsTranslate()))
            _operation = TransformGizmoOperation.Translate;
        if (Widgets.ToolbarButton(_rotateObjectsLabel, "Rotate Objects"u8, IsRotate()))
            _operation = TransformGizmoOperation.Rotate;
        if (Widgets.ToolbarButton(_scaleObjectsLabel, "Scale Objects"u8, IsScale()))
            _operation = TransformGizmoOperation.Scale;

        Widgets.ToolbarSeparator();

        ReadOnlySpan<byte> localTitle = _isLocal
            ? "Transform in local object space"u8
            : "Transform in world space"u8;
        if (Widgets.ToolbarButton(_transformSpaceLabel, localTitle, _isLocal))
            _isLocal = !_isLocal;

        ReadOnlySpan<byte> pivotedTitle = _isPivoted
            ? "Transform around individual objects' pivots"u8
            : "Transform around the center of selection"u8;
        if (Widgets.ToolbarButton(_transformPivotLabel, pivotedTitle, _isPivoted))
            _isPivoted = !_isPivoted;

        Widgets.ToolbarSeparator();

        return true;
    }

    public override void WriteIniSettings(ImGuiTextBuffer output)
    {
        IniHelpers.WriteIntToIni(output, "TransformGizmo.IsLocal", _isLocal ? 1 : 0);
        IniHelpers.WriteIntToIni(output, "TransformGizmo.IsPivoted", _isPivoted ? 1 : 0);
        IniHelpers.WriteIntToIni(output, "TransformGizmo.Operation", (int)_operation);
    }

    public override void ReadIniSettings(string line)
    {
        var value = IniHelpers.ReadIntFromIni(line, "TransformGizmo.IsLocal");
        if (value.HasValue)
            _isLocal = value.Value != 0;
        value = IniHelpers.ReadIntFromIni(line, "TransformGizmo.IsPivoted");
        if (value.HasValue)
            _isPivoted = value.Value != 0;
        value = IniHelpers.ReadIntFromIni(line, "TransformGizmo.Operation");
        if (value.HasValue)
        {
            _operation = (TransformGizmoOperation)Math.Clamp(
                value.Value, (int)TransformGizmoOperation.None, (int)TransformGizmoOperation.Scale);
        }
    }

    private void SetHotkeyLabel(HotkeyManager hotkeyManager, EditorHotkey hotkey)
    {
        _hotkeyLabel.Set(hotkeyManager.GetHotkeyLabel(hotkey));
    }

    private static bool IsKeyDown(Key key)
    {
        var imguiKey = ImGui.ImGuiImplSDL2KeyEventToImGuiKey(
            (int)key, (int)Input.GetScancodeFromKey(key));
        return ImGui.IsKeyDown(imguiKey);
    }

    private readonly WeakReference<SettingsPage> _settings;

    private WeakReference<Scene>? _selectionScene;
    private uint _selectionRevision;
    private TransformNodesGizmo? _transformNodesGizmo;

    private bool _isLocal;
    private bool _isPivoted;
    private TransformGizmoOperation _operation = TransformGizmoOperation.Translate;

    private readonly CString _hotkeyLabel = new();
}
