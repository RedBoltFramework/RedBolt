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

using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
using RedBolt.Editor.Core;
using RedBolt.Editor.Project;
using RedBolt.IO;

// TODO: port Foundation_KeyBindingsPage when editor plugins are ported.
// void Foundation_KeyBindingsPage(Context* context, SettingsTab* settingsTab)
// {
//     auto project = settingsTab->GetProject();
//     auto settingsManager = project->GetSettingsManager();
//     settingsManager->AddPage(MakeShared<KeyBindingsPage>(context));
// }

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
