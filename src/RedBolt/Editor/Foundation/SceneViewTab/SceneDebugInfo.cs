//
// Copyright (c) 2017-2020 the rbfx project.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

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
        if (Widgets.ToolbarButton("\uf188"u8, "Toggle Debug HUD"u8, _hudVisible))
            ToggleHud();
        if (Widgets.ToolbarButton("\uf84c"u8, "Toggle Wireframe"u8, _drawWireframe))
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
