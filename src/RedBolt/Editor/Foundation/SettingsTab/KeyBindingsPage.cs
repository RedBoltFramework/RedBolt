// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Editor.Foundation;

using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
using RedBolt.Editor.Core;
using RedBolt.Editor.Project;
using RedBolt.IO;

/// <summary>Tab that displays project settings.</summary>
public class KeyBindingsPage : SettingsPage
{
    public KeyBindingsPage()
    {
    }

    public override void Dispose()
    {
        _text.Dispose();
        base.Dispose();
    }

    /// <summary>Implement SettingsPage.</summary>
    public override string UniqueName => "Editor.KeyBindings";

    public override bool IsSerializable => false;

    public override void SerializeInBlock(Archive archive)
    {
    }

    public override void RenderSettings()
    {
        var project = GetSubsystem<Project>();
        var hotkeyManager = project.HotkeyManager;

        ImGui.Text("TODO: No, you cannot rebind those yet. PRs are welcome."u8);

        if (ImGui.BeginTable("Hotkeys"u8, 3))
        {
            ImGui.TableSetupColumn("Command"u8);
            ImGui.TableSetupColumn("Press"u8);
            ImGui.TableSetupColumn("Hold"u8);
            ImGui.TableHeadersRow();

            foreach (var (command, _) in hotkeyManager.Bindings)
            {
                var hotkey = hotkeyManager.GetHotkey(command);
                var qualifiers = hotkey.GetQualifiersString();
                var press = hotkey.GetPressString();
                var hold = hotkey.GetHoldString();

                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                _text.Set(command);
                ImGui.Text(_text.ContentBytes);

                ImGui.TableNextColumn();
                if (press.Length != 0)
                    _text.FormatInPlace($"{qualifiers}{press}");
                else
                    _text.Clear();
                ImGui.Text(_text.ContentBytes);

                ImGui.TableNextColumn();
                if (hold.Length != 0)
                    _text.FormatInPlace($"{qualifiers}{hold}");
                else
                    _text.Clear();
                ImGui.Text(_text.ContentBytes);
            }
            ImGui.EndTable();
        }
    }

    private readonly CString _text = new();
}
