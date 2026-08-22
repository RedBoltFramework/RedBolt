// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Editor.Foundation;

using System;

using RedBolt.Bindings.DearImGui;
using RedBolt.Editor.Core;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.SystemUI;

/// <summary>Add-on to manage debug HUD in scene.</summary>
public class SceneDebugInfo : SceneViewAddon
{
    private static readonly EditorHotkey HotkeyToggleHud =
        new EditorHotkey("DebugInfo.ToggleHud");

    public struct Settings : ISimpleSettingsPageValues
    {
        public string UniqueName => "Editor.Scene:DebugInfo";

        public void SerializeInBlock(Archive archive)
        {
        }

        public void RenderSettings()
        {
        }
    }

    public class SettingsPage : SimpleSettingsPage<Settings>
    {
    }

    public SceneDebugInfo(SceneViewTab owner, SettingsPage settings)
        : base(owner)
    {
        _settings = new WeakReference<SettingsPage>(settings);

        var hotkeyManager = owner.GetHotkeyManager();
        hotkeyManager.BindHotkey(this, HotkeyToggleHud, static self => self.ToggleHud());
    }

    public override void Dispose()
    {
        _settings.SetTarget(null!);
        base.Dispose();
    }

    /// <summary>Commands.</summary>
    public void ToggleHud() => _hudVisible = !_hudVisible;

    /// <summary>Implement SceneViewAddon.</summary>
    public override string GetUniqueName() => "DebugInfo";

    public override int GetToolbarPriority() => 100;

    public override void Render(SceneViewPage scenePage)
    {
        var hud = GetSubsystem<DebugHud>();
        if (hud is not null && _hudVisible && _owner.TryGetTarget(out var owner))
        {
            var position = owner.GetContentPosition();
            ImGui.SetCursorScreenPos(position.ToVector2());
            hud.RenderUI(DebugHudMode.All);
        }

        if (scenePage.Renderer.Camera is { } camera)
            camera.SetFillMode(_drawWireframe ? FillMode.Wireframe : FillMode.Solid);
    }

    public override void ApplyHotkeys(HotkeyManager hotkeyManager)
    {
        hotkeyManager.InvokeFor(this);
    }

    public override bool RenderTabContextMenu() => true;

    public override bool RenderToolbar()
    {
        if (Widgets.ToolbarButton("\uf188##SceneViewDebugHud"u8, "Toggle Debug HUD"u8, _hudVisible))
            ToggleHud();
        if (Widgets.ToolbarButton("\uf84c##ToggleWireframe"u8, "Toggle Wireframe"u8, _drawWireframe))
            _drawWireframe = !_drawWireframe;

        Widgets.ToolbarSeparator();

        return true;
    }

    public override void WriteIniSettings(ImGuiTextBuffer output)
    {
        IniHelpers.WriteIntToIni(output, "SceneDebugInfo.HudVisible", _hudVisible ? 1 : 0);
        IniHelpers.WriteIntToIni(output, "SceneDebugInfo.DrawWireframe", _drawWireframe ? 1 : 0);
    }

    public override void ReadIniSettings(string line)
    {
        var value = IniHelpers.ReadIntFromIni(line, "SceneDebugInfo.HudVisible");
        if (value.HasValue)
            _hudVisible = value.Value != 0;

        value = IniHelpers.ReadIntFromIni(line, "SceneDebugInfo.DrawWireframe");
        if (value.HasValue)
            _drawWireframe = value.Value != 0;
    }

    private readonly WeakReference<SettingsPage> _settings;

    private bool _hudVisible;
    private bool _drawWireframe;
}
