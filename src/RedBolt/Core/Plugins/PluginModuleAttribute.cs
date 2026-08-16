// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Plugins;

/// <summary>Describes a managed plugin assembly without requiring it to be loaded during discovery.</summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class PluginModuleAttribute : Attribute
{
    public PluginModuleAttribute(string name, Type applicationType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        ApplicationType = applicationType ?? throw new ArgumentNullException(nameof(applicationType));
    }

    public string Name { get; }

    public Type ApplicationType { get; }

    public PluginReloadPolicy ReloadPolicy { get; set; } = PluginReloadPolicy.Collectible;

    /// <summary>Load the plugin whenever its assembly is present, without adding it to project settings.</summary>
    public bool AutoLoad { get; set; }
}

public enum PluginReloadPolicy
{
    Collectible,
    ProcessResident,
}
