// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Editor.Foundation;

using System;
using System.Collections.Generic;

using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
using RedBolt.Editor.Core;
using RedBolt.Editor.Project;
using RedBolt.IO;
using RedBolt.Plugins;
using RedBolt.SystemUI;
using RedBolt.Utility;

/// <summary>Tab that displays project settings.</summary>
public class LaunchPage : SettingsPage
{
    public LaunchPage()
    {
        _launchManager = new WeakReference<LaunchManager>(GetSubsystem<Project>().LaunchManager);
    }

    public override void Dispose()
    {
        _mainPlugin.Dispose();
        _title.Dispose();
        base.Dispose();
    }

    /// <summary>Implement SettingsPage.</summary>
    public override string UniqueName => "Project.Launch";

    public override bool IsSerializable => false;

    public override void SerializeInBlock(Archive archive)
    {
    }

    private static readonly CString _addConfigurationLabel = new($"{Icons.SquarePlus} Add new launch configuration");

    public override void RenderSettings()
    {
        if (!_launchManager.TryGetTarget(out var launchManager))
            return;

        _pendingConfigurationsRemoved.Clear();

        var oldHash = MakeHash(launchManager.Configurations);

        var index = 0;
        foreach (var config in launchManager.MutableConfigurations)
        {
            RenderConfiguration(index, config);
            ImGui.Separator();
            ++index;
        }

        if (ImGui.Button(_addConfigurationLabel))
        {
            using var config = new LaunchConfiguration();
            config.Name = GetUnusedConfigurationName();
            launchManager.AddConfiguration(config);
        }

        _pendingConfigurationsRemoved.Sort();
        for (var i = _pendingConfigurationsRemoved.Count - 1; i >= 0; --i)
            launchManager.RemoveConfiguration(_pendingConfigurationsRemoved[i]);

        var newHash = MakeHash(launchManager.Configurations);
        if (oldHash != newHash)
        {
            var project = GetSubsystem<Project>();
            project.MarkUnsaved();
        }

        static uint MakeHash(IReadOnlyList<LaunchConfiguration> configurations)
        {
            if (IntPtr.Size == 4)
            {
                var result32 = 0u;
                foreach (var configuration in configurations)
                {
                    var hash = configuration.ToHash();
                    result32 ^= hash + 0x9e3779b9u + (result32 << 6) + (result32 >> 2);
                }
                return result32;
            }

            var result64 = 0ul;
            foreach (var configuration in configurations)
            {
                var hash = configuration.ToHash();
                result64 ^= hash + 0x9e3779b97f4a7c15ul + (result64 << 6) + (result64 >> 2);
            }

            var foldedResult = (uint)result64;
            var highValue = (uint)(result64 >> 32);
            if (highValue != 0)
                foldedResult ^= highValue + 0x9e3779b9u + (foldedResult << 6) + (foldedResult >> 2);
            return foldedResult;
        }
    }

    private string GetUnusedConfigurationName()
    {
        _launchManager.TryGetTarget(out var launchManager);

        var name = string.Empty;
        for (var i = 0; i < 1024; ++i)
        {
            name = $"Configuration {i}";
            if (!launchManager!.HasConfiguration(name))
                break;
        }
        return name;
    }

    private void RenderConfiguration(int index, LaunchConfiguration config)
    {
        using var guard = new IdScopeGuard(index);

        if (ImGui.Button(Icons.TrashCanU8))
            _pendingConfigurationsRemoved.Add(index);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Remove this launch configuration"u8);

        ImGui.SameLine();

        _launchManager.TryGetTarget(out var launchManager);
        var isBadName = config.Name.Length == 0
            || !ReferenceEquals(launchManager!.FindConfiguration(config.Name), config);
        _title.FormatInPlace($"{(isBadName ? Icons.TriangleExclamation : string.Empty)}{config.Name}");
        if (!ImGui.CollapsingHeader(
                _title.ContentBytes, ImGuiTreeNodeFlags.ImGuiTreeNodeFlagsDefaultOpen))
        {
            return;
        }

        Widgets.InputText("Name"u8, ref config.Name);
        RenderMainPlugin(ref config.MainPlugin);
    }

    private void RenderMainPlugin(ref string mainPlugin)
    {
        _mainPlugin.Set(mainPlugin.Length != 0 ? mainPlugin : LaunchConfiguration.UnspecifiedName);
        if (!ImGui.BeginCombo("Main Plugin"u8, _mainPlugin.ContentBytes))
            return;

        _mainPlugin.Set(LaunchConfiguration.UnspecifiedName);
        if (ImGui.Selectable(_mainPlugin.ContentBytes, mainPlugin.Length == 0))
            mainPlugin = string.Empty;

        var pluginManager = GetSubsystem<PluginManager>();
        foreach (var pluginName in pluginManager.LoadedPlugins)
        {
            var plugin = pluginManager.GetPlugin(pluginName, true);
            if (plugin?.IsExecutable != true)
                continue;

            _mainPlugin.Set(pluginName);
            if (ImGui.Selectable(_mainPlugin.ContentBytes, pluginName == mainPlugin))
                mainPlugin = pluginName;
        }
        ImGui.EndCombo();
    }

    private readonly WeakReference<LaunchManager> _launchManager;

    private readonly List<int> _pendingConfigurationsRemoved = [];
    private readonly CString _title = new();
    private readonly CString _mainPlugin = new();
}
