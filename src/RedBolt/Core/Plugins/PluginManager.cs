// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Plugins;

using Microsoft.Extensions.Logging;

using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.IO;
using RedBolt.Logging;

/// <summary>Owns an ordered stack of runtime plugins and commits changes between frames.</summary>
public sealed class PluginManager : RedObject
{
    public PluginManager()
    {
        SubscribeToEvent<CoreEvents.EndFramePrivateEvent>((ref CoreEvents.EndFramePrivateEvent _) => Commit());
    }

    public IReadOnlyList<string> LoadedPlugins => _loadedPlugins;

    public uint PluginListRevision { get; private set; }

    public bool IsReloadPending => _reloadPending;

    public bool IsStarted => _stack?.IsStarted == true;

    public PluginApplication? MainPlugin => _stack?.MainPlugin;

    public Action QuitApplicationCallback { get; set; } = static () =>
        Context.Instance.GetSubsystem<Engine>()?.Exit();

    public void Configure(EngineParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        _mainPlugin = parameters.MainPlugin;
        _autoReload = parameters.ReloadPlugins;
        SetPluginsLoaded(SplitList(parameters.Plugins));
        Commit();
    }

    public bool AddPlugin(Plugin plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        if (!_available.TryAdd(plugin.Name, plugin))
            return false;
        if (plugin.AutoLoad)
            _reloadPending = true;
        return true;
    }

    public bool AddStaticPlugin(string name, Func<PluginApplication> factory) =>
        AddPlugin(new StaticPlugin(name, factory));

    public PluginApplication? GetPluginApplication(string name, bool loadedOnly = false)
    {
        if (!_available.TryGetValue(name, out var plugin))
            return null;
        if (plugin.Application is null && !loadedOnly && !plugin.Load())
            return null;
        return plugin.Application;
    }

    public IReadOnlyList<string> EnumerateAvailablePlugins() =>
        _available.Keys.Order(StringComparer.Ordinal).ToArray();

    public bool IsPluginAutoLoaded(string name) =>
        _available.TryGetValue(name, out var plugin) && plugin.AutoLoad;

    public void SetPluginsLoaded(IEnumerable<string> plugins)
    {
        ArgumentNullException.ThrowIfNull(plugins);
        _loadedPlugins.Clear();
        _loadedPlugins.AddRange(plugins.Where(static name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.Ordinal));
        _reloadPending = true;
        PluginListRevision = global::System.Math.Max(1u, PluginListRevision + 1);
    }

    public void Reload() => _reloadPending = true;

    public void StartApplication()
    {
        if (_startPending || IsStarted && !_stopPending)
            return;
        _startPending = true;
    }

    public void StopApplication()
    {
        if (_startPending)
        {
            _startPending = false;
            return;
        }
        if (IsStarted)
            _stopPending = true;
    }

    public void QuitApplication() => QuitApplicationCallback();

    public string? GetReloadBlockedReason()
    {
        if (_stack?.IsStarted == true && !_stack.IsSuspendSupported)
            return "Started plugins do not support hot reload; stop playing first.";
        return _available.Values.FirstOrDefault(static plugin => plugin.IsOutOfDate && !plugin.IsReadyToReload) is not null
            ? "Plugin assemblies are still being written."
            : null;
    }

    public void Commit()
    {
        if (_stopPending && _stack is not null)
        {
            _stack.Stop();
            _stopPending = false;
        }

        if (_autoReload && _available.Values.Any(static plugin => plugin.IsOutOfDate)
            && GetReloadBlockedReason() is null)
        {
            _reloadPending = true;
        }

        if (_reloadPending)
        {
            if (GetReloadBlockedReason() is not null)
                return;
            ReloadStack();
            _reloadPending = false;
        }

        if (_startPending)
        {
            _stack ??= CreateStack();
            _stack.Start(_mainPlugin);
            _startPending = false;
        }
    }

    public override void SerializeInBlock(Archive archive)
    {
        ArgumentNullException.ThrowIfNull(archive);
        ArchiveSerialization.SerializeStringVector(archive, "LoadedPlugins", _loadedPlugins);
        if (archive.IsInput)
            SetPluginsLoaded(_loadedPlugins.ToArray());
    }

    public override void Dispose()
    {
        _stack?.Dispose();
        _stack = null;
        foreach (var plugin in _available.Values)
        {
            plugin.Unload();
            plugin.Dispose();
        }
        _available.Clear();
        base.Dispose();
    }

    private void ReloadStack()
    {
        SendEvent<EngineEvents.BeginPluginReloadEvent>();
        var wasStarted = _stack?.IsStarted == true;
        var state = wasStarted ? _stack!.Suspend() : null;
        _stack?.Dispose();
        _stack = null;
        var changed = _available.Values.Where(static plugin => plugin.IsOutOfDate).ToArray();
        try
        {
            foreach (var plugin in changed)
            {
                if (!plugin.BeginReload())
                    throw new InvalidOperationException($"Plugin '{plugin.Name}' could not prepare a replacement.");
            }

            _stack = CreateStack();
            if (wasStarted)
                _stack.Resume(state!);
            foreach (var plugin in changed)
                plugin.CommitReload();
        }
        catch (Exception exception)
        {
            Log.GetLogger().LogError(exception, "Plugin reload failed; restoring the previous generation.");
            _stack?.Dispose();
            _stack = null;
            for (var index = changed.Length - 1; index >= 0; --index)
                changed[index].RollbackReload();
            _stack = CreateStack();
            if (wasStarted)
                _stack.Resume(state!);
        }
        finally
        {
            SendEvent<EngineEvents.EndPluginReloadEvent>();
        }
    }

    private PluginStack CreateStack()
    {
        var modules = new List<Plugin>();
        var names = _available.Values
            .Where(static plugin => plugin.AutoLoad)
            .OrderBy(static plugin => plugin.LoadOrder)
            .ThenBy(static plugin => plugin.Name, StringComparer.Ordinal)
            .Select(static plugin => plugin.Name)
            .Concat(_loadedPlugins)
            .Distinct(StringComparer.Ordinal);
        foreach (var name in names)
        {
            if (!_available.TryGetValue(name, out var plugin))
            {
                Log.GetLogger().LogWarning("Plugin '{Plugin}' was not found.", name);
                continue;
            }
            if (!plugin.Load())
            {
                Log.GetLogger().LogError("Plugin '{Plugin}' could not be loaded.", name);
                continue;
            }
            modules.Add(plugin);
        }
        return new PluginStack(modules);
    }

    private static string[] SplitList(string value) => value.Split(
        [';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private readonly Dictionary<string, Plugin> _available = new(StringComparer.Ordinal);
    private readonly List<string> _loadedPlugins = [];
    private PluginStack? _stack;
    private string _mainPlugin = string.Empty;
    private bool _autoReload;
    private bool _startPending;
    private bool _stopPending;
    private bool _reloadPending;
}

internal sealed class PluginStack : IDisposable
{
    public PluginStack(IReadOnlyList<Plugin> plugins)
    {
        _plugins = plugins;
        var loaded = 0;
        try
        {
            foreach (var plugin in _plugins)
            {
                plugin.Application!.LoadPlugin();
                ++loaded;
            }
        }
        catch
        {
            for (var index = loaded - 1; index >= 0; --index)
                _plugins[index].Application!.UnloadPlugin();
            throw;
        }
    }

    public bool IsStarted { get; private set; }

    public bool IsSuspendSupported => _plugins.All(static plugin => plugin.Application!.IsSuspendSupported);

    public PluginApplication? MainPlugin { get; private set; }

    public void Start(string mainPlugin)
    {
        if (IsStarted)
            return;
        MainPlugin = _plugins.Select(static plugin => plugin.Application!)
            .FirstOrDefault(application => application.IsMain && application.PluginName == mainPlugin)
            ?? _plugins.Select(static plugin => plugin.Application!).FirstOrDefault(static application => application.IsMain);
        foreach (var plugin in _plugins)
            plugin.Application!.StartApplication(ReferenceEquals(plugin.Application, MainPlugin));
        IsStarted = true;
    }

    public Dictionary<string, PluginState> Suspend()
    {
        var result = new Dictionary<string, PluginState>(StringComparer.Ordinal);
        for (var index = _plugins.Count - 1; index >= 0; --index)
        {
            var plugin = _plugins[index];
            result[plugin.Name] = new PluginState(plugin.Version, plugin.Application!.SuspendApplication());
        }
        IsStarted = false;
        return result;
    }

    public void Resume(IReadOnlyDictionary<string, PluginState> state)
    {
        foreach (var plugin in _plugins)
        {
            state.TryGetValue(plugin.Name, out var previous);
            plugin.Application!.ResumeApplication(previous.State, previous.Version != plugin.Version);
        }
        IsStarted = true;
    }

    public void Stop()
    {
        if (!IsStarted)
            return;
        for (var index = _plugins.Count - 1; index >= 0; --index)
            _plugins[index].Application!.StopApplication();
        IsStarted = false;
    }

    public void Dispose()
    {
        Stop();
        for (var index = _plugins.Count - 1; index >= 0; --index)
            _plugins[index].Application!.UnloadPlugin();
    }

    private readonly IReadOnlyList<Plugin> _plugins;
}

internal readonly record struct PluginState(uint Version, string? State);
