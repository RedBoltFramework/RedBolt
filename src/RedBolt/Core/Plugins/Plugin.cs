namespace RedBolt.Plugins;

using RedBolt.Core;

/// <summary>Loadable module that owns one plugin application.</summary>
public abstract class Plugin : RedObject
{
    protected Plugin(string name, PluginReloadPolicy reloadPolicy, bool autoLoad = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        ReloadPolicy = reloadPolicy;
        AutoLoad = autoLoad;
    }

    public string Name { get; }

    public PluginReloadPolicy ReloadPolicy { get; }

    public bool AutoLoad { get; }

    public PluginApplication? Application { get; protected set; }

    public uint Version { get; protected set; }

    public abstract bool Load();

    public abstract void Unload();

    public virtual bool IsOutOfDate => false;

    public virtual bool IsReadyToReload => true;

    public virtual bool BeginReload()
    {
        Unload();
        return Load();
    }

    public virtual void CommitReload() { }

    public virtual void RollbackReload() { }
}

internal sealed class StaticPlugin : Plugin
{
    public StaticPlugin(string name, Func<PluginApplication> factory)
        : base(name, PluginReloadPolicy.ProcessResident)
    {
        _factory = factory;
    }

    public override bool Load()
    {
        Application ??= _factory();
        Application.PluginName = Name;
        Version = global::System.Math.Max(1u, Version);
        return true;
    }

    public override void Unload()
    {
        Application?.Dispose();
        Application = null;
    }

    private readonly Func<PluginApplication> _factory;
}
