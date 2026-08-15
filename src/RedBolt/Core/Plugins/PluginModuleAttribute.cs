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
}

public enum PluginReloadPolicy
{
    Collectible,
    ProcessResident,
}
