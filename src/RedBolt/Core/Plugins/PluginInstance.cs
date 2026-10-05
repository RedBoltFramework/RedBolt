// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Plugins;

using RedBolt.Core;

/// <summary>Wrapper that manages loading and unloading of one plugin application.</summary>
public abstract class PluginInstance : RedObject
{
    protected PluginInstance(string name, PluginReloadPolicy reloadPolicy, bool autoLoad = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        ReloadPolicy = reloadPolicy;
        AutoLoad = autoLoad;
    }

    public string Name { get; }

    public PluginReloadPolicy ReloadPolicy { get; }

    public bool AutoLoad { get; }

    public virtual int LoadOrder => 0;

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

internal sealed class StaticPluginInstance : PluginInstance
{
    public StaticPluginInstance(string name, Func<PluginApplication> factory)
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
