namespace RedBolt.Editor.Foundation;

using System.Collections.Generic;
using System.Linq;

using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
using RedBolt.Editor.Core;
using RedBolt.Editor.Project;
using RedBolt.Engine;
using RedBolt.IO;
using RedBolt.UserCode.Desktop;

/// <summary>Project plugin selection and reload controls.</summary>
public sealed class PluginsPage : SettingsPage
{
    public override string UniqueName => "Project.Plugins";

    public override bool IsSerializable => false;

    public override void SerializeInBlock(Archive archive) { }

    public override void Dispose()
    {
        _label.Dispose();
        base.Dispose();
    }

    public override void RenderSettings()
    {
        var project = GetSubsystem<Project>();
        var manager = project.PluginManager;
        if (_revision != manager.PluginListRevision && !_hasChanges)
            Synchronize(manager.LoadedPlugins, manager.PluginListRevision);

        if (ImGui.Button("\uf021 Refresh"u8))
        {
            var engine = GetSubsystem<Engine>();
            ManagedPluginCatalog.FromParameters(engine.Parameters).Populate(manager);
        }
        ImGui.SameLine();
        if (ImGui.Button("\uf2f1 Reload"u8))
            manager.Reload();

        ImGui.Separator();
        foreach (var plugin in manager.EnumerateAvailablePlugins())
        {
            var automatic = manager.IsPluginAutoLoaded(plugin);
            var enabled = automatic || _loadedPlugins.Contains(plugin);
            _label.Set(automatic ? $"{plugin} (automatic)" : plugin);
            ImGui.BeginDisabled(automatic);
            if (ImGui.Checkbox(_label.ContentBytes, ref enabled))
            {
                if (enabled)
                    _loadedPlugins.Add(plugin);
                else
                    _loadedPlugins.Remove(plugin);
                _hasChanges = true;
                project.MarkUnsaved();
            }
            ImGui.EndDisabled();
        }

        ImGui.BeginDisabled(!_hasChanges);
        if (ImGui.Button("\uf00c Apply"u8))
        {
            manager.SetPluginsLoaded(_loadedPlugins);
            _revision = manager.PluginListRevision;
            _hasChanges = false;
        }
        ImGui.SameLine();
        if (ImGui.Button("\uf00d Discard"u8))
            Synchronize(manager.LoadedPlugins, manager.PluginListRevision);
        ImGui.EndDisabled();

        if (manager.GetReloadBlockedReason() is { } reason)
        {
            _label.Set(reason);
            ImGui.TextUnformatted(_label.ContentBytes);
        }
    }

    private void Synchronize(IEnumerable<string> plugins, uint revision)
    {
        _loadedPlugins.Clear();
        _loadedPlugins.AddRange(plugins);
        _revision = revision;
        _hasChanges = false;
    }

    private readonly List<string> _loadedPlugins = [];
    private readonly CString _label = new();
    private uint _revision;
    private bool _hasChanges;
}
