//
// Copyright (c) 2008-2017 the Urho3D project.
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

namespace RedBolt.SystemUI;

using System;
using System.Collections.Generic;

using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.RenderAPI;

[Flags]
public enum DebugHudMode : uint
{
    None = 0x0,
    Stats = 0x1,
    Mode = 0x2,
    All = 0x7,
}

/// <summary>Displays rendering stats and profiling information.</summary>
public class DebugHud : RedObject
{
    private static readonly string[] QualityTexts = ["Low", "Med", "High"];
    private static readonly string[] FilterModeTexts = ["Nearest", "Bilinear", "Trilinear", "Anisotropic"];

    private const uint FpsUpdateIntervalMs = 500;

    /// <summary>Construct.</summary>
    public DebugHud()
    {
        SubscribeToEvent<GraphicsEvents.EndAllViewsRenderEvent>(OnRenderDebugUI);
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        UnsubscribeFromAllEvents();
        _textBuffer.Dispose();
        base.Dispose();
    }

    /// <summary>Set elements to show.</summary>
    /// <param name="mode">A combination of show flags.</param>
    public void SetMode(DebugHudMode mode)
    {
        _mode = mode;
    }

    /// <summary>Cycle through elements.</summary>
    public void CycleMode()
    {
        switch (_mode)
        {
        case DebugHudMode.None:
            SetMode(DebugHudMode.Stats);
            break;
        case DebugHudMode.Stats:
            SetMode(DebugHudMode.Mode);
            break;
        case DebugHudMode.Mode:
            SetMode(DebugHudMode.All);
            break;
        case DebugHudMode.All:
        default:
            SetMode(DebugHudMode.None);
            break;
        }
    }

    /// <summary>Toggle elements.</summary>
    /// <param name="mode">A combination of show flags.</param>
    public void Toggle(DebugHudMode mode)
    {
        SetMode(GetMode() ^ mode);
    }

    /// <summary>Toggle all elements.</summary>
    public void ToggleAll()
    {
        Toggle(DebugHudMode.All);
    }

    /// <summary>Return currently shown elements.</summary>
    /// <returns>A combination of show flags.</returns>
    public DebugHudMode GetMode() => _mode;

    /// <summary>Set application-specific stats.</summary>
    /// <param name="label">A title of stat to be displayed.</param>
    /// <param name="stats">A variant value to be displayed next to the specified label.</param>
    public void SetAppStats(string label, in Variant stats)
    {
        SetAppStats(label, stats.ToString());
    }

    /// <summary>Set application-specific stats.</summary>
    /// <param name="label">A title of stat to be displayed.</param>
    /// <param name="stats">A string value to be displayed next to the specified label.</param>
    public void SetAppStats(string label, string stats)
    {
        _appStats[label] = stats;
    }

    /// <summary>Reset application-specific stats. Return true if it was erased successfully.</summary>
    /// <param name="label">A title of stat to be reset.</param>
    public bool ResetAppStats(string label)
    {
        return _appStats.Remove(label);
    }

    /// <summary>Clear all application-specific stats.</summary>
    public void ClearAppStats()
    {
        _appStats.Clear();
    }

    /// <summary>Render system ui.</summary>
    public void RenderUI(DebugHudMode mode)
    {
        if (mode == DebugHudMode.None)
            return;

        var renderer = GetSubsystem<Renderer>();
        var graphics = GetSubsystem<Graphics>();
        var renderDevice = GetSubsystem<RenderDevice>();

        if ((mode & DebugHudMode.Stats) != 0)
        {
            ref readonly var stats = ref renderer.GetFrameStats();

            if (_fpsTimer.GetMSec(false) > FpsUpdateIntervalMs)
            {
                _fps = unchecked((uint)MathDefs.Round(GetSubsystem<Time>().GetFramesPerSecond()));
                (_numChangedAnimations[0], _numChangedAnimations[1]) =
                    (_numChangedAnimations[1], _numChangedAnimations[0]);
                _numChangedAnimations[1] = 0;
                _fpsTimer.Reset();
            }

            _numChangedAnimations[1] += stats.ChangedAnimations;

            var leftOffset = ImGui.GetCursorPos().X;

            _textBuffer.FormatInPlace($"FPS {_fps}");
            ImGui.TextUnformatted(_textBuffer.ContentBytes);
            ImGui.SetCursorPosX(leftOffset);
            _textBuffer.FormatInPlace($"Triangles {renderDevice.GetMaxStats().NumPrimitives}");
            ImGui.TextUnformatted(_textBuffer.ContentBytes);
            ImGui.SetCursorPosX(leftOffset);
            _textBuffer.FormatInPlace($"Draws {renderDevice.GetMaxStats().NumDraws}");
            ImGui.TextUnformatted(_textBuffer.ContentBytes);
            ImGui.SetCursorPosX(leftOffset);
            _textBuffer.FormatInPlace($"Dispatches {renderDevice.GetMaxStats().NumDispatches}");
            ImGui.TextUnformatted(_textBuffer.ContentBytes);
            ImGui.SetCursorPosX(leftOffset);
            _textBuffer.FormatInPlace($"Views {renderer.GetNumViews()}");
            ImGui.TextUnformatted(_textBuffer.ContentBytes);
            ImGui.SetCursorPosX(leftOffset);
            _textBuffer.FormatInPlace($"Lights {renderer.GetNumLights()}");
            ImGui.TextUnformatted(_textBuffer.ContentBytes);
            ImGui.SetCursorPosX(leftOffset);
            _textBuffer.FormatInPlace($"Shadowmaps {renderer.GetNumShadowMaps()}");
            ImGui.TextUnformatted(_textBuffer.ContentBytes);
            ImGui.SetCursorPosX(leftOffset);
            _textBuffer.FormatInPlace($"Occluders {renderer.GetNumOccluders()}");
            ImGui.TextUnformatted(_textBuffer.ContentBytes);
            ImGui.SetCursorPosX(leftOffset);
            _textBuffer.FormatInPlace($"Animations {stats.Animations}({_numChangedAnimations[0]})");
            ImGui.TextUnformatted(_textBuffer.ContentBytes);
            ImGui.SetCursorPosX(leftOffset);

            foreach (var item in _appStats)
            {
                _textBuffer.FormatInPlace($"{item.Key} {item.Value}");
                ImGui.TextUnformatted(_textBuffer.ContentBytes);
                ImGui.SetCursorPosX(leftOffset);
            }
        }

        if ((mode & DebugHudMode.Mode) != 0)
        {
            // TODO: Add more stats?
            var style = ImGui.GetStyle();
            ImGui.SetCursorPos(new Vector2f(
                style.WindowPadding.X,
                ImGui.GetWindowSize().Y - ImGui.GetStyle().WindowPadding.Y - ImGui.GetFontSize()));
            _textBuffer.FormatInPlace(
                $"API:{graphics.GetApiName()} | Tex:{QualityTexts[(int)renderer.GetTextureQuality()]} | Filter:{FilterModeTexts[(int)renderer.GetTextureFilterMode()]}");
            ImGui.TextUnformatted(_textBuffer.ContentBytes);
        }
    }

    /// <summary>Render debug hud on to entire viewport.</summary>
    private void OnRenderDebugUI(ref GraphicsEvents.EndAllViewsRenderEvent eventData)
    {
        if (!ImGui.ImGuiBackendIsWithinFrameScope())
            return;

        var viewport = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(viewport.Pos);
        ImGui.SetNextWindowSize(viewport.Size);
        ImGui.SetNextWindowViewport(viewport.Id);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColWindowBg, 0u);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColBorder, 0u);
        var flags = ImGuiWindowFlags.ImGuiWindowFlagsNoResize | ImGuiWindowFlags.ImGuiWindowFlagsNoTitleBar
            | ImGuiWindowFlags.ImGuiWindowFlagsNoMove | ImGuiWindowFlags.ImGuiWindowFlagsNoInputs
            | ImGuiWindowFlags.ImGuiWindowFlagsNoScrollbar;
        // TODO: port nullable p_open when generated bindings support it.
        // if (ui::Begin("DebugHud", nullptr, flags))
        var open = true;
        if (ImGui.Begin("DebugHud"u8, ref open, flags))
            RenderUI(_mode);
        ImGui.End();
        ImGui.PopStyleColor(2);
    }

    /// <summary>Hashmap containing application specific stats.</summary>
    private readonly SortedDictionary<string, string> _appStats = new(StringComparer.Ordinal);
    /// <summary>Current shown-element mode.</summary>
    private DebugHudMode _mode = DebugHudMode.None;
    /// <summary>FPS timer.</summary>
    private readonly Timer _fpsTimer = new();
    /// <summary>Calculated fps.</summary>
    private uint _fps;
    private readonly uint[] _numChangedAnimations = new uint[2];
    private readonly CString _textBuffer = new();
}
