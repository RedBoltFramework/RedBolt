// Copyright (c) 2008-2017 the Urho3D project.
// Copyright (c) 2017-2024 the rbfx project.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.SystemUI;

using System;

using RedBolt.Bindings.DearImGui;
using RedBolt.Math;

/// <summary>State guard for paired ID scope calls.</summary>
public readonly struct IdScopeGuard : IDisposable
{
    public IdScopeGuard(ReadOnlySpan<byte> id)
    {
        ImGui.PushID(id);
    }

    public IdScopeGuard(IntPtr id)
    {
        ImGui.PushID(id);
    }

    public IdScopeGuard(int id)
    {
        ImGui.PushID(id);
    }

    public void Dispose()
    {
        ImGui.PopID();
    }
}

public readonly struct ColorScopeGuard : IDisposable
{
    public ColorScopeGuard(ImGuiCol id, Vector4f color, bool enabled = true)
    {
        _numColors = enabled ? 1 : 0;
        if (enabled)
            ImGui.PushStyleColor(id, color);
    }

    public ColorScopeGuard(ReadOnlySpan<(ImGuiCol Id, Vector4f Color)> colors, bool enabled = true)
    {
        _numColors = enabled ? colors.Length : 0;
        if (enabled)
        {
            foreach (var (id, color) in colors)
                ImGui.PushStyleColor(id, color);
        }
    }

    public ColorScopeGuard(ImGuiCol id, uint color, bool enabled = true)
    {
        _numColors = enabled ? 1 : 0;
        if (enabled)
            ImGui.PushStyleColor(id, color);
    }

    public ColorScopeGuard(ReadOnlySpan<(ImGuiCol Id, uint Color)> colors, bool enabled = true)
    {
        _numColors = enabled ? colors.Length : 0;
        if (enabled)
        {
            foreach (var (id, color) in colors)
                ImGui.PushStyleColor(id, color);
        }
    }

    public ColorScopeGuard(ImGuiCol id, Color color, bool enabled = true)
    {
        _numColors = enabled ? 1 : 0;
        if (enabled)
            ImGui.PushStyleColor(id, new Vector4f(color.R, color.G, color.B, color.A));
    }

    public ColorScopeGuard(ReadOnlySpan<(ImGuiCol Id, Color Color)> colors, bool enabled = true)
    {
        _numColors = enabled ? colors.Length : 0;
        if (enabled)
        {
            foreach (var (id, color) in colors)
                ImGui.PushStyleColor(id, new Vector4f(color.R, color.G, color.B, color.A));
        }
    }

    public void Dispose()
    {
        if (_numColors > 0)
            ImGui.PopStyleColor(_numColors);
    }

    private readonly int _numColors;
}
