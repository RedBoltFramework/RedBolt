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

    public Plugin? MainPlugin => _stack?.MainPlugin;

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

    public bool AddPlugin(PluginInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        if (!_available.TryAdd(instance.Name, instance))
            return false;
        if (instance.AutoLoad)
            _reloadPending = true;
        return true;
    }

    public bool AddStaticPlugin(string name, Func<Plugin> factory) =>
        AddPlugin(new StaticPluginInstance(name, factory));

    public Plugin? GetPlugin(string name, bool loadedOnly = false)
    {
        if (!_available.TryGetValue(name, out var instance))
            return null;
        if (instance.Plugin is null && !loadedOnly && !instance.Load())
            return null;
        return instance.Plugin;
    }

    public IReadOnlyList<string> EnumerateAvailablePlugins() =>
        _available.Keys.Order(StringComparer.Ordinal).ToArray();

    public bool IsPluginAutoLoaded(string name) =>
        _available.TryGetValue(name, out var instance) && instance.AutoLoad;

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
        return _available.Values.FirstOrDefault(static instance => instance.IsOutOfDate && !instance.IsReadyToReload) is not null
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

        if (_autoReload && _available.Values.Any(static instance => instance.IsOutOfDate)
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
        foreach (var instance in _available.Values)
        {
            instance.Unload();
            instance.Dispose();
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
        var changed = _available.Values.Where(static instance => instance.IsOutOfDate).ToArray();
        try
        {
            foreach (var instance in changed)
            {
                if (!instance.BeginReload())
                    throw new InvalidOperationException($"Plugin '{instance.Name}' could not prepare a replacement.");
            }

            _stack = CreateStack();
            if (wasStarted)
                _stack.Resume(state!);
            foreach (var instance in changed)
                instance.CommitReload();
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
        var instances = new List<PluginInstance>();
        var names = _available.Values
            .Where(static instance => instance.AutoLoad)
            .OrderBy(static instance => instance.LoadOrder)
            .ThenBy(static instance => instance.Name, StringComparer.Ordinal)
            .Select(static instance => instance.Name)
            .Concat(_loadedPlugins)
            .Distinct(StringComparer.Ordinal);
        foreach (var name in names)
        {
            if (!_available.TryGetValue(name, out var instance))
            {
                Log.GetLogger().LogWarning("Plugin '{Plugin}' was not found.", name);
                continue;
            }
            if (!instance.Load())
            {
                Log.GetLogger().LogError("Plugin '{Plugin}' could not be loaded.", name);
                continue;
            }
            instances.Add(instance);
        }
        return new PluginStack(instances);
    }

    private static string[] SplitList(string value) => value.Split(
        [';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private readonly Dictionary<string, PluginInstance> _available = new(StringComparer.Ordinal);
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
    public PluginStack(IReadOnlyList<PluginInstance> instances)
    {
        _instances = instances;
        var loaded = 0;
        try
        {
            foreach (var instance in _instances)
            {
                instance.Plugin!.LoadPlugin();
                ++loaded;
            }
        }
        catch
        {
            for (var index = loaded - 1; index >= 0; --index)
                _instances[index].Plugin!.UnloadPlugin();
            throw;
        }
    }

    public bool IsStarted { get; private set; }

    public bool IsSuspendSupported => _instances.All(static instance => instance.Plugin!.IsSuspendSupported);

    public Plugin? MainPlugin { get; private set; }

    public void Start(string mainPlugin)
    {
        if (IsStarted)
            return;
        MainPlugin = _instances.Select(static instance => instance.Plugin!)
            .FirstOrDefault(plugin => plugin.IsExecutable && plugin.PluginName == mainPlugin)
            ?? _instances.Select(static instance => instance.Plugin!).FirstOrDefault(static plugin => plugin.IsExecutable);
        foreach (var instance in _instances)
            instance.Plugin!.StartApplication(ReferenceEquals(instance.Plugin, MainPlugin));
        IsStarted = true;
    }

    public Dictionary<string, PluginState> Suspend()
    {
        var result = new Dictionary<string, PluginState>(StringComparer.Ordinal);
        for (var index = _instances.Count - 1; index >= 0; --index)
        {
            var instance = _instances[index];
            result[instance.Name] = new PluginState(instance.Version, instance.Plugin!.SuspendApplication());
        }
        IsStarted = false;
        return result;
    }

    public void Resume(IReadOnlyDictionary<string, PluginState> state)
    {
        foreach (var instance in _instances)
        {
            state.TryGetValue(instance.Name, out var previous);
            instance.Plugin!.ResumeApplication(previous.State, previous.Version != instance.Version);
        }
        IsStarted = true;
    }

    public void Stop()
    {
        if (!IsStarted)
            return;
        for (var index = _instances.Count - 1; index >= 0; --index)
            _instances[index].Plugin!.StopApplication();
        IsStarted = false;
    }

    public void Dispose()
    {
        Stop();
        for (var index = _instances.Count - 1; index >= 0; --index)
            _instances[index].Plugin!.UnloadPlugin();
    }

    private readonly IReadOnlyList<PluginInstance> _instances;
}

internal readonly record struct PluginState(uint Version, string? State);
