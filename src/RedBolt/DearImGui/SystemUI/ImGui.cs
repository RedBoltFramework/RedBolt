// Copyright (c) 2008-2017 the Urho3D project.
// Copyright (c) 2017-2024 the rbfx project.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.SystemUI;

using System;

using RedBolt.Bindings.DearImGui;

/// <summary>State guard for paired ID scope calls.</summary>
public class IdScopeGuard : IDisposable
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
