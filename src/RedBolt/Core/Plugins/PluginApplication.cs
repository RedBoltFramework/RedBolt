// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Plugins;

using System;
using System.IO;

using RedBolt.Core;
using RedBolt.IO;

/// <summary>Base class for code hosted by <see cref="PluginManager"/>.</summary>
public abstract class PluginApplication : RedObject
{
    public string PluginName { get; internal set; } = string.Empty;

    /// <summary>Directory containing this plugin's managed entry assembly.</summary>
    public string PluginDirectory { get; internal set; } = AppContext.BaseDirectory;

    /// <summary>Resolve a path relative to <see cref="PluginDirectory"/>.</summary>
    public string GetPluginPath(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        if (Path.IsPathRooted(relativePath))
            throw new ArgumentException("Plugin-relative path must not be rooted.", nameof(relativePath));
        return Path.GetFullPath(Path.Combine(PluginDirectory, relativePath));
    }

    public virtual bool IsMain => false;

    public virtual bool IsSuspendSupported => true;

    public bool IsLoaded { get; private set; }

    public bool IsStarted { get; private set; }

    internal void LoadPlugin()
    {
        if (IsLoaded)
            return;
        Load();
        IsLoaded = true;
    }

    internal void UnloadPlugin()
    {
        if (!IsLoaded)
            return;
        if (IsStarted)
            StopApplication();
        Unload();
        IsLoaded = false;
    }

    internal void StartApplication(bool isMain)
    {
        if (IsStarted)
            return;
        Start(isMain);
        IsStarted = true;
    }

    internal void StopApplication()
    {
        if (!IsStarted)
            return;
        Stop();
        IsStarted = false;
    }

    internal string SuspendApplication()
    {
        using var archive = new Base64OutputArchive();
        Suspend(archive);
        archive.Flush();
        IsStarted = false;
        return archive.GetBase64();
    }

    internal void ResumeApplication(string? state, bool differentVersion)
    {
        if (state is null)
            Resume(null, differentVersion);
        else
        {
            using var archive = new Base64InputArchive(state);
            Resume(archive, differentVersion);
            archive.Flush();
        }
        IsStarted = true;
    }

    protected virtual void Load() { }

    protected virtual void Unload() { }

    protected virtual void Start(bool isMain) { }

    protected virtual void Stop() { }

    protected virtual void Suspend(Archive archive) { }

    protected virtual void Resume(Archive? archive, bool differentVersion) { }
}

/// <summary>A plugin application that may be selected as the process entry point.</summary>
public abstract class MainPluginApplication : PluginApplication
{
    public sealed override bool IsMain => true;

    public override bool IsSuspendSupported => false;
}
