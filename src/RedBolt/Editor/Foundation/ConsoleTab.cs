// Copyright (c) 2017-2024 the rbfx project.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Editor.Foundation;

using System;

using RedBolt.Bindings.DearImGui;
using RedBolt.Editor.Core;
using RedBolt.Editor.Project;
using RedBolt.Logging;
using ConsoleSubsystem = RedBolt.SystemUI.Console;

/// <summary>Tab that displays application log and enables console input.</summary>
public class ConsoleTab : EditorTab
{
    private static readonly LogLevel[] LogLevels =
    [
        LogLevel.Trace,
        LogLevel.Debug,
        LogLevel.Info,
        LogLevel.Warning,
        LogLevel.Error,
    ];

    private static readonly string[] LogLevelNames = ["TRACE", "DEBUG", "INFO", "WARN", "ERROR"];
    private static readonly byte[][] LogLevelNameBytes =
    [
        "TRACE"u8.ToArray(),
        "DEBUG"u8.ToArray(),
        "INFO"u8.ToArray(),
        "WARN"u8.ToArray(),
        "ERROR"u8.ToArray(),
    ];

    public ConsoleTab()
        : base("Console", "2c1b8e59-3e21-4a14-bc20-d35af0ba5031", EditorTabFlag.OpenByDefault,
            EditorTabPlacement.DockBottom)
    {
    }

    /// <summary>Implement EditorTab.</summary>
    public override void RenderContent()
    {
        var console = GetSubsystem<ConsoleSubsystem>();

        var font = Project.GetMonoFont();
        if (font is not null)
            ImGui.PushFont(font, font.LegacySize);
        console.RenderContent();
        if (font is not null)
            ImGui.PopFont();
    }

    public override void RenderContextMenuItems()
    {
        var console = GetSubsystem<ConsoleSubsystem>();

        _contextMenuSeparator.Reset();

        if (ImGui.MenuItem("Clear"u8))
        {
            console.Clear();
        }

        if (ImGui.BeginMenu("Levels"u8))
        {
            foreach (var level in LogLevels)
            {
                var visible = console.GetLevelVisible(level);
                if (ImGui.MenuItem(LogLevelNameBytes[(int)level], default, visible))
                    console.SetLevelVisible(level, !visible);
            }
            ImGui.EndMenu();
        }

        _contextMenuSeparator.Add();
    }

    public override void WriteIniSettings(ImGuiTextBuffer output)
    {
        base.WriteIniSettings(output);

        var console = GetSubsystem<ConsoleSubsystem>();
        if (console is not null)
        {
            foreach (var level in LogLevels)
            {
                IniHelpers.WriteIntToIni(
                    output, $"Show_{LogLevelNames[(int)level]}", console.GetLevelVisible(level) ? 1 : 0);
            }
        }
    }

    public override void ReadIniSettings(string line)
    {
        base.ReadIniSettings(line);

        var console = GetSubsystem<ConsoleSubsystem>();
        if (console is not null)
        {
            foreach (var level in LogLevels)
            {
                var value = IniHelpers.ReadIntFromIni(line, $"Show_{LogLevelNames[(int)level]}");
                if (value.HasValue)
                    console.SetLevelVisible(level, value.Value != 0);
            }
        }
    }
}
