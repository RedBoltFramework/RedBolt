// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Editor.Foundation;

using RedBolt.Editor.Project;

/// <summary>Dockable editor presentation of the engine profiler.</summary>
public sealed class ProfilerTab : EditorTab
{
    private readonly global::RedBolt.DearImGui.ProfilerWindow _window = new();

    public ProfilerTab()
        : base("Profiler", "f0664dca-82fd-4f22-a033-d3322103d54e", EditorTabFlag.None,
            EditorTabPlacement.DockBottom)
    {
    }

    public override void Dispose()
    {
        _window.Dispose();
        base.Dispose();
    }

    public override void RenderContent() => _window.RenderContent();
}
