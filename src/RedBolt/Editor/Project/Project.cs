//
// Copyright (c) 2017-2020 the rbfx project.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

namespace RedBolt.Editor.Project;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Editor.Assets;
using RedBolt.Editor.Core;
using RedBolt.Engine;
using RedBolt.IO;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.Plugins;
using RedBolt.Resource;
using RedBolt.SystemUI;
using RedBolt.UserCode.Desktop;
using RedBolt.Utility;

using EngineSubsystem = global::RedBolt.Engine.Engine;
using File = global::System.IO.File;
using ResourceType = global::RedBolt.Resource.Resource;

/// <summary>Project configuration flags.</summary>
[Flags]
public enum ProjectFlag
{
    None = 0,
    /// <summary>Project should not modify any files unless explicitly requested by the user.</summary>
    ReadOnly = 1 << 0,
    /// <summary>Project should not spawn any additional processes unless explicitly requested by the user.</summary>
    SingleProcess = 1 << 1,
}

/// <summary>Result of the graceful project close.</summary>
public enum CloseProjectResult
{
    Undefined,
    Closed,
    Canceled
}

/// <summary>Helper class to keep and restore state of ResourceCache.</summary>
public sealed class ResourceCacheGuard : IDisposable
{
    public ResourceCacheGuard()
    {
        var virtualFileSystem = Context.Instance.GetSubsystem<VirtualFileSystem>()!;
        for (var i = 0u; i < virtualFileSystem.NumMountPoints(); ++i)
        {
            var mountPoint = virtualFileSystem.GetMountPoint(i)!;
            _oldResourceDirs.Add(mountPoint);
            if (mountPoint is MountedDirectory directory)
            {
                var resourceDir = directory.GetDirectory();
                // VirtualFileSystem resolves later mounts first, so retain the last
                // matching directory rather than the first fallback mount.
                if (resourceDir.EndsWith("/CoreData/", StringComparison.Ordinal))
                    CoreData = resourceDir;
                if (resourceDir.EndsWith("/EditorData/", StringComparison.Ordinal))
                    EditorData = resourceDir;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        var virtualFileSystem = Context.Instance.GetSubsystem<VirtualFileSystem>()!;
        var currentMountPoints = new List<MountPoint>();
        for (var i = 0u; i < virtualFileSystem.NumMountPoints(); ++i)
            currentMountPoints.Add(virtualFileSystem.GetMountPoint(i)!);

        virtualFileSystem.UnmountAll(false);
        for (var i = currentMountPoints.Count - 1; i >= 0; --i)
        {
            if (!_oldResourceDirs.Contains(currentMountPoints[i]))
                currentMountPoints[i].Dispose();
        }
        foreach (var mountPoint in _oldResourceDirs)
            virtualFileSystem.Mount(mountPoint);

        _oldResourceDirs.Clear();
        _disposed = true;
    }

    public string CoreData { get; private set; } = string.Empty;

    public string EditorData { get; private set; } = string.Empty;

    private readonly List<MountPoint> _oldResourceDirs = [];
    private bool _disposed;
}

/// <summary>File type analysis context.</summary>
public class AnalyzeFileContext : IDisposable
{
    public AbstractFile? BinaryFile;
    public XMLFile? XmlFile;
    public JSONFile? JsonFile;

    public bool HasXMLRoot(string root)
    {
        if (XmlFile is null)
            return false;

        using var xmlRoot = XmlFile.GetRoot();
        return string.Equals(xmlRoot.Name, root, StringComparison.OrdinalIgnoreCase);
    }

    public bool HasXMLRoot(IEnumerable<string> roots)
    {
        foreach (var root in roots)
        {
            if (HasXMLRoot(root))
                return true;
        }
        return false;
    }

    public void Dispose()
    {
        JsonFile?.Dispose();
        JsonFile = null;
        XmlFile?.Dispose();
        XmlFile = null;
        BinaryFile?.Dispose();
        BinaryFile = null;
        GC.SuppressFinalize(this);
    }
}

/// <summary>Main class for all Editor logic related to the project folder.</summary>
public class Project : RedObject
{
    public delegate void AnalyzeFileCallback(ResourceFileDescriptor descriptor, AnalyzeFileContext context);
    public delegate void CommandExecutedCallback(bool success, string output);
    public delegate void FileSavedCallback(string fileName, string resourceName, ref bool needReload);
    public delegate void CommandHandler(Project sender, string command, string args, ref bool processed);

    private static readonly EditorHotkey HotkeySaveProject =
        new EditorHotkey("Global.SaveProject").Ctrl().Shift().Press(RedBolt.Input.Key.S);

    private static ImFont? _monoFont;
    private static int _numActiveProjects;
    private const string SelfIniEntry = "Project";

    public event Action<Project>? OnInitialized;
    public event Action<Project>? OnShallowSaved;
    public event Action<Project>? OnRenderProjectMenu;
    public event Action<Project>? OnRenderProjectToolbar;
    public event Action<EditorTab?, ProjectRequest>? OnRequest;
    public event CommandHandler? OnCommand;

    public Project(string projectPath, string settingsJsonPath, ProjectFlag flags)
    {
        var engine = GetSubsystem<EngineSubsystem>();
        _isHeadless = engine.IsHeadless;
        _flags = flags;
        _isXR = engine.Parameters.XR;
        _projectPath = AddTrailingSlash(projectPath.Replace('\\', '/'));
        _cachePath = _projectPath + "Cache/";
        _tempPath = _projectPath + "Temp/";
        _artifactsPath = _projectPath + "Artifacts/";
        _projectJsonPath = _projectPath + "Project.json";
        _settingsJsonPath = settingsJsonPath;
        _cacheJsonPath = _projectPath + "Cache.json";
        _uiIniPath = _projectPath + "ui.ini";
        _gitIgnorePath = _projectPath + ".gitignore";
        _previewPngPath = _projectPath + "Preview.png";
        _dataPath = _projectPath + "Data/";
        _uiIniPathCString = new CString(_uiIniPath);
        _oldCacheState = new ResourceCacheGuard();
        _hotkeyManager = new HotkeyManager();
        _undoManager = new UndoManager();
        _settingsManager = new SettingsManager();
        _defaultPluginManager = GetSubsystem<PluginManager>();
        _pluginManager = new PluginManager();
        ManagedPluginCatalog.FromParameters(engine.Parameters).Populate(_pluginManager);
        _launchManager = new LaunchManager();
        _toolManager = new ToolManager();
        _closeDialog = new CloseDialog();

        _initializationPending = true;

        Debug.Assert(_numActiveProjects == 0);
        Context.RegisterSubsystem<Project>(this);
        ++_numActiveProjects;

        Context.RemoveSubsystem<PluginManager>();
        Context.RegisterSubsystem(_pluginManager);
        _pluginManager.QuitApplicationCallback = static () => { };
        _pluginManager.Configure(engine.Parameters);

        SubscribeToEvent<EngineEvents.EndPluginReloadEvent>(
            (ref EngineEvents.EndPluginReloadEvent _) => _pluginReloadEndTime = DateTime.UtcNow);

        if (!_isHeadless && (_flags & ProjectFlag.ReadOnly) == 0)
            ImGui.GetIO().IniFilename = _uiIniPathCString.Pointer;

        InitializeHotkeys();
        EnsureDirectoryInitialized();
        InitializeResourceCache();

        // Delay asset manager creation until project is ready
        _assetManager = new AssetManager();
        _assetManager.OnInitialized += _ => _initializationPending = false;

        IgnoreFileNamePattern("*.user.json");
        IgnoreFileNamePattern("*.blend1");

        _settingsManager.AddPage(_toolManager);

        _settingsManager.LoadFile(_settingsJsonPath);
        _assetManager.LoadFile(_cacheJsonPath);

        using (var projectJsonFile = new JSONFile())
        {
            projectJsonFile.LoadFile(new FileIdentifier(_projectJsonPath));
            projectJsonFile.LoadObjectCallback(SerializeProject);
        }

        _pluginManager.Commit();
        ApplyPlugins();

        if (_firstInitialization)
            InitializeDefaultProject();
        if ((_flags & ProjectFlag.ReadOnly) == 0)
            UpdateDefaultAssetPipeline();
    }

    public override void Dispose()
    {
        // TODO: port XR session shutdown when VirtualReality is available.
        // auto virtualReality = GetSubsystem<VirtualReality>();
        // if (virtualReality && isXR_)
        //     virtualReality->ShutdownSession();

        var cache = GetSubsystem<ResourceCache>();
        cache.ReleaseAllResources(true);

        --_numActiveProjects;
        Debug.Assert(_numActiveProjects == 0);

        if (!_isHeadless)
            ImGui.GetIO().IniFilename = 0;

        OnCommand = null;
        OnRequest = null;
        OnRenderProjectToolbar = null;
        OnRenderProjectMenu = null;
        OnShallowSaved = null;
        OnInitialized = null;

        _closeDialog.Dispose();
        _toolManager.Dispose();
        _launchManager.Dispose();
        _pluginManager.Dispose();
        _undoManager.Dispose();
        _settingsManager.Dispose();
        _hotkeyManager.Dispose();
        _assetManager.Dispose();
        _oldCacheState.Dispose();
        _saveHotkeyLabel.Dispose();
        _assetsProgressText.Dispose();
        _uiIniPathCString.Dispose();
        base.Dispose();
    }

    public override void SerializeInBlock(Archive archive)
    {
        if (!archive.IsInput || archive.HasElementOrBlock("PluginManager"))
        {
            using var pluginBlock = archive.OpenUnorderedBlock("PluginManager");
            _pluginManager.SerializeInBlock(archive);
        }
        if (archive.IsInput && !archive.HasElementOrBlock("LaunchManager"))
            return;

        using var block = archive.OpenUnorderedBlock("LaunchManager");
        _launchManager.SerializeInBlock(archive);
    }

    /// <summary>Execute the command line in the context of the project.</summary>
    /// <remarks>Execution will be postponed until the project is initialized.</remarks>
    public void ExecuteCommand(string command, bool exitOnCompletion = false)
    {
        if (command.Trim().Length == 0)
        {
            Log.GetLogger().LogWarning("Empty command is ignored");
            return;
        }

        if (_initialized)
            ProcessCommand(command, exitOnCompletion);
        else
            _pendingCommands.Add((command, exitOnCompletion));
    }

    /// <summary>Execute the command line in another process.</summary>
    public bool ExecuteRemoteCommand(string command, out string output)
    {
        var engine = GetSubsystem<EngineSubsystem>();
        var programFileName = Environment.ProcessPath ?? string.Empty;

        string[] arguments =
        [
            "--quiet",
            "--log",
            "ERROR",
            "--headless",
            "--exit",
            "--read-only",
            "--command",
            command,
            "--prefix-paths",
            engine.Parameters.ResourcePrefixPaths,
            _projectPath,
        ];

        var exitCode = ProcessUtils.SystemRun(programFileName, arguments, out output);
        if (exitCode != 0)
        {
            Log.GetLogger().LogError(
                "Failed to execute remote command \"{Command}\" with exit code {ExitCode}: {Output}",
                command, exitCode, output);
            return false;
        }
        return true;
    }

    /// <summary>Execute the command line in another process.</summary>
    public bool ExecuteRemoteCommand(string command)
    {
        return ExecuteRemoteCommand(command, out _);
    }

    /// <summary>Execute the command line in another process asynchronously.</summary>
    public void ExecuteRemoteCommandAsync(string command, CommandExecutedCallback callback)
    {
        var remoteCommand = new PendingRemoteCommand
        {
            Callback = callback,
            Result = Task.Run(() =>
            {
                var success = ExecuteRemoteCommand(command, out var output);
                return (success, output);
            }),
        };
        _pendingRemoteCommands.Add(remoteCommand);
    }

    /// <summary>Called right before destructor.</summary>
    /// <remarks>Perform all complicated stuff here because Project is still available for plugins as subsystem.</remarks>
    public void Destroy()
    {
        // Always save shallow data on close
        SaveShallowOnly();

        ProcessDelayedSaves(true);

        for (var i = _tabs.Count - 1; i >= 0; --i)
            _tabs[i].Dispose();
        _tabs.Clear();
        _sortedTabs.Clear();

        Context.RemoveSubsystem<PluginManager>();
        Context.RegisterSubsystem(_defaultPluginManager);
    }

    /// <summary>Request graceful close of the project. Called multiple times during close sequence.</summary>
    public CloseProjectResult CloseGracefully()
    {
        // If result is ready, return it now and reset state
        if (_closeProjectResult != CloseProjectResult.Undefined)
        {
            var result = _closeProjectResult;
            _closeProjectResult = CloseProjectResult.Undefined;
            return result;
        }

        // Wait if dialog is already open
        if (_closeDialog.IsActive)
            return CloseProjectResult.Undefined;

        // Collect unsaved items
        var hasUnsavedCookedAssets = _assetManager.IsProcessing;

        var unsavedItems = new List<string>();
        if (_hasUnsavedChanges)
            unsavedItems.Add("[Project]");
        if (hasUnsavedCookedAssets)
            unsavedItems.Add("[Cooked Assets]");
        foreach (var tab in _tabs)
            tab.EnumerateUnsavedItems(unsavedItems);

        // If nothing to save, close immediately
        if (unsavedItems.Count == 0)
            return CloseProjectResult.Closed;

        // Open popup otherwise
        var request = new CloseResourceRequest
        {
            ResourceNames = unsavedItems,
            OnSave = () =>
            {
                Save();
                _closeProjectResult = CloseProjectResult.Closed;
            },
            OnDiscard = () => _closeProjectResult = CloseProjectResult.Closed,
            OnCancel = () => _closeProjectResult = CloseProjectResult.Canceled,
        };
        _closeDialog.SetSaveEnabled(!hasUnsavedCookedAssets);
        _closeDialog.RequestClose(request);
        return CloseProjectResult.Undefined;
    }

    /// <summary>Request graceful close of the resource.</summary>
    public void CloseResourceGracefully(CloseResourceRequest request)
    {
        _closeDialog.RequestClose(request);
    }

    /// <summary>Process global request.</summary>
    public void ProcessRequest(ProjectRequest request, EditorTab? sender = null)
    {
        _pendingRequests.Add(new PendingRequest(request, sender));
    }

    /// <summary>Add callback for file analysis.</summary>
    public void AddAnalyzeFileCallback(AnalyzeFileCallback callback)
    {
        _analyzeFileCallbacks.Add(callback);
    }

    /// <summary>Return file descriptor for specified file.</summary>
    public ResourceFileDescriptor GetResourceDescriptor(string resourceName, string fileName = "")
    {
        var cache = GetSubsystem<ResourceCache>();

        using var context = new AnalyzeFileContext
        {
            BinaryFile = cache.GetFile(resourceName, false),
        };

        if (context.BinaryFile is not null && resourceName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
        {
            context.XmlFile = new XMLFile();
            context.XmlFile.Load(context.BinaryFile);
            context.BinaryFile.Seek(0);
        }

        if (context.BinaryFile is not null && resourceName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            context.JsonFile = new JSONFile();
            context.JsonFile.Load(context.BinaryFile);
            context.BinaryFile.Seek(0);
        }

        var result = new ResourceFileDescriptor
        {
            LocalName = Path.GetFileName(resourceName.Replace('\\', '/')),
            ResourceName = resourceName,
            FileName = fileName,
        };

        if (context.BinaryFile is not null && result.FileName.Length == 0)
            result.FileName = context.BinaryFile.AbsoluteName;
        if (result.FileName.Length == 0)
            result.FileName = _dataPath + resourceName;

        result.IsDirectory = context.BinaryFile is null;
        result.IsAutomatic = result.FileName.StartsWith(_cachePath, StringComparison.Ordinal);

        foreach (var callback in _analyzeFileCallbacks)
            callback(result, context);

        return result;
    }

    /// <summary>Update and render main window with tabs.</summary>
    public void Render()
    {
        const float tint = 0.15f;
        if (_isHighlightEnabled)
        {
            ImGui.PushStyleColor(ImGuiCol.ImGuiColTab, new Vector4f(0.26f, 0.26f + tint, 0.26f, 0.40f));
            ImGui.PushStyleColor(ImGuiCol.ImGuiColTabHovered, new Vector4f(0.31f, 0.31f + tint, 0.31f, 1.00f));
            ImGui.PushStyleColor(ImGuiCol.ImGuiColTabSelected, new Vector4f(0.28f, 0.28f + tint, 0.28f, 1.00f));
            ImGui.PushStyleColor(ImGuiCol.ImGuiColTabDimmed, new Vector4f(0.17f, 0.17f + tint, 0.17f, 1.00f));
            ImGui.PushStyleColor(ImGuiCol.ImGuiColTabDimmedSelected, new Vector4f(0.26f, 0.26f + tint, 0.26f, 1.00f));
        }

        try
        {
            if (!_isHeadless)
            {
                _hotkeyManager.Update();
                _hotkeyManager.InvokeFor(_hotkeyManager);
                if (_areGlobalHotkeysEnabled)
                    _hotkeyManager.InvokeFor(this);

                _dockspaceId = ImGui.GetID("Root"u8);
                ImGui.DockSpace(_dockspaceId);

                if (_pendingResetLayout)
                    ResetLayout();
            }

            if (!_assetManagerInitialized && !_pluginManager.IsReloadPending)
            {
                _assetManagerInitialized = true;
                _toolManager.Update();
                _assetManager.Initialize((_flags & ProjectFlag.ReadOnly) != 0);
            }

            _assetManager.Update();

            var initialFocusPending = false;
            if (!_initialized && !_initializationPending)
            {
                _initialized = true;
                initialFocusPending = true;

                _pluginReloadEndTime = null;

                OnInitialized?.Invoke(this);

                foreach (var (command, exitOnCompletion) in _pendingCommands)
                    ProcessCommand(command, exitOnCompletion);
                _pendingCommands.Clear();
            }

            if (!_isHeadless)
            {
                foreach (var tab in _tabs)
                    tab.PreRenderUpdate();
                foreach (var tab in _tabs)
                    tab.Render();
                FocusedTab?.ApplyHotkeys(_hotkeyManager);
                foreach (var tab in _tabs)
                    tab.PostRenderUpdate();

                _closeDialog.Render();

                if (initialFocusPending)
                {
                    foreach (var tab in _tabs)
                    {
                        if (tab.IsOpen && (tab.Flags & EditorTabFlag.FocusOnStart) != 0)
                            tab.Focus(true);
                    }
                }
            }

            ProcessDelayedSaves();
            ProcessPendingRequests();
            ProcessPendingRemoteCommands();
        }
        finally
        {
            if (_isHighlightEnabled)
                ImGui.PopStyleColor(5);
        }
    }

    /// <summary>Update and render toolbar.</summary>
    public void RenderToolbar()
    {
        if (Widgets.ToolbarButton("\uf0c7"u8, "Save Project"u8))
            Save();
        OnRenderProjectToolbar?.Invoke(this);

        Widgets.ToolbarSeparator();

        RootFocusedTab?.RenderToolbar();

        RenderAssetsToolbar();
        RenderPluginReloadToolbar();
        RenderSavePendingToolbar();
    }

    /// <summary>Update and render project menu.</summary>
    public void RenderProjectMenu()
    {
        _saveHotkeyLabel.Set(_hotkeyManager.GetHotkeyLabel(HotkeySaveProject));
        if (ImGui.MenuItem("\uf0c7 Save Project"u8, _saveHotkeyLabel.ContentBytes))
            Save();
        OnRenderProjectMenu?.Invoke(this);
    }

    /// <summary>Update and render the rest of menu bar.</summary>
    public void RenderMainMenu()
    {
        RootFocusedTab?.RenderMenu();

        if (RootFocusedTab is not null && ImGui.BeginMenu("Tab"u8))
        {
            RootFocusedTab.RenderContextMenuItems();
            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu("Window"u8))
        {
            foreach (var pair in _sortedTabs)
            {
                var tab = pair.Value;
                if (ImGui.MenuItem(tab.TitleBytes, ReadOnlySpan<byte>.Empty, tab.IsOpen))
                {
                    if (tab.IsOpen)
                        tab.Close();
                    else
                        tab.Focus();
                }
            }
            ImGui.EndMenu();
        }
    }

    /// <summary>Save file after delay and ignore reload event.</summary>
    public void SaveFileDelayed(
        string fileName, string resourceName, byte[] bytes, FileSavedCallback? onSaved = null)
    {
        _delayedFileSaves[resourceName] = new PendingFileSave(fileName, bytes, onSaved, null);
    }

    public void SaveFileDelayed(ResourceType resource, FileSavedCallback? onSaved = null)
    {
        _delayedFileSaves[resource.Name] =
            new PendingFileSave(resource.AbsoluteFileName, null, onSaved, resource);
    }

    /// <summary>Mark files with specified name pattern as internal and ignore them in UI.</summary>
    public void IgnoreFileNamePattern(string pattern)
    {
        if (_ignoredFileNames.Add(pattern))
            _ignoredFileNameRegexes.Add(PatternToRegex(pattern));
    }

    /// <summary>Return whether the file name is ignored.</summary>
    public bool IsFileNameIgnored(string fileName)
    {
        foreach (var regex in _ignoredFileNameRegexes)
        {
            if (regex.IsMatch(fileName))
                return true;
        }
        return false;
    }

    /// <summary>Add new tab. Avoid calling it in realtime.</summary>
    public void AddTab(EditorTab tab)
    {
        if (_isHeadless)
            return;

        _tabs.Add(tab);
        _sortedTabs[tab.Title] = tab;
    }

    /// <summary>Find first tab of matching type.</summary>
    public T? FindTab<T>()
        where T : EditorTab
    {
        foreach (var tab in _tabs)
        {
            if (tab is T derivedTab)
                return derivedTab;
        }
        return null;
    }

    /// <summary>Set whether the global hotkeys are enabled.</summary>
    public void SetGlobalHotkeysEnabled(bool enabled)
    {
        _areGlobalHotkeysEnabled = enabled;

        var io = ImGui.GetIO();
        if (enabled)
            io.ConfigFlags |= ImGuiConfigFlags.ImGuiConfigFlagsNavEnableKeyboard;
        else
            io.ConfigFlags &= ~ImGuiConfigFlags.ImGuiConfigFlagsNavEnableKeyboard;
    }

    /// <summary>Set whether the UI highlight is enabled.</summary>
    public void SetHighlightEnabled(bool enabled) => _isHighlightEnabled = enabled;

    /// <summary>Set current launch configuration name.</summary>
    public void SetLaunchConfigurationName(string name) => _currentLaunchConfiguration = name;

    /// <summary>Return current launch configuration name.</summary>
    public string LaunchConfigurationName => _currentLaunchConfiguration;

    /// <summary>Return current launch configuration.</summary>
    public LaunchConfiguration? LaunchConfiguration =>
        _launchManager.FindConfiguration(_currentLaunchConfiguration);

    /// <summary>Return name of random temporary directory.</summary>
    public string GetRandomTemporaryPath()
    {
        return $"{_tempPath}{ProcessUtils.GenerateUUID()}/";
    }

    /// <summary>Create temporary directory that will be automatically deleted when the handler is destroyed.</summary>
    public TemporaryDir CreateTemporaryDir()
    {
        return new TemporaryDir(GetRandomTemporaryPath());
    }

    /// <summary>Mark project itself as having unsaved changes.</summary>
    public void MarkUnsaved() => _hasUnsavedChanges = true;

    /// <summary>Return whether the project itself has unsaved changes.</summary>
    public bool HasUnsavedChanges => _hasUnsavedChanges;

    /// <summary>Commands.</summary>
    public void SaveShallowOnly()
    {
        if ((_flags & ProjectFlag.ReadOnly) != 0)
            return;

        if (!_isHeadless)
            ImGui.SaveIniSettingsToDisk(_uiIniPathCString.ContentBytes);
        _settingsManager.SaveFile(_settingsJsonPath);
        _assetManager.SaveFile(_cacheJsonPath);

        foreach (var tab in _tabs)
        {
            if (tab is ResourceEditorTab resourceTab)
                resourceTab.SaveShallow();
        }

        OnShallowSaved?.Invoke(this);
    }

    public void SaveProjectOnly()
    {
        using var projectJsonFile = new JSONFile();
        if (projectJsonFile.SaveObjectCallback(SerializeProject))
            projectJsonFile.SaveFile(new FileIdentifier(_projectJsonPath));

        // Save .gitignore file once so user can edit it
        if (!File.Exists(_gitIgnorePath))
            SaveGitIgnore();

        _hasUnsavedChanges = false;
    }

    public void SaveResourcesOnly(bool forceSave = true)
    {
        foreach (var tab in _tabs)
        {
            if (tab is ResourceEditorTab resourceTab)
                resourceTab.SaveAllResources(forceSave);
        }
        ProcessDelayedSaves(true);
    }

    public void Save()
    {
        SaveProjectOnly();
        SaveShallowOnly();
        SaveResourcesOnly();
    }

    public void ReadIniSettings(string entry, string line)
    {
        if (string.Equals(entry, SelfIniEntry, StringComparison.Ordinal))
        {
            var value = IniHelpers.ReadStringFromIni(line, "LaunchConfiguration");
            if (value is not null)
                _currentLaunchConfiguration = value;
        }

        foreach (var tab in _tabs)
        {
            if (string.Equals(entry, tab.IniEntry, StringComparison.Ordinal))
                tab.ReadIniSettings(line);
        }
    }

    public void WriteIniSettings(ImGuiTextBuffer output)
    {
        output.Append("\n[Project][Project]\n"u8);
        IniHelpers.WriteStringToIni(output, "LaunchConfiguration", _currentLaunchConfiguration);

        foreach (var tab in _tabs)
        {
            output.Append("\n[Project]["u8);
            output.Append(Encoding.UTF8.GetBytes(tab.IniEntry));
            output.Append("]\n"u8);
            tab.WriteIniSettings(output);
        }
    }

    public ProjectFlag Flags => _flags;
    public string ProjectPath => _projectPath;
    public string CoreDataPath => _oldCacheState.CoreData;
    public string DataPath => _dataPath;
    public string CachePath => _cachePath;
    public string ArtifactsPath => _artifactsPath;
    public string PreviewPngPath => _previewPngPath;

    public AssetManager AssetManager => _assetManager;
    public HotkeyManager HotkeyManager => _hotkeyManager;
    public SettingsManager SettingsManager => _settingsManager;
    public UndoManager UndoManager => _undoManager;
    public PluginManager PluginManager => _pluginManager;
    public LaunchManager LaunchManager => _launchManager;
    public ToolManager ToolManager => _toolManager;

    /// <summary>Contribute an optional transformer to newly created and existing default asset pipelines.</summary>
    public IDisposable RegisterDefaultAssetTransformer<T>()
        where T : AssetTransformer
    {
        var className = typeof(T).GetClassName();
        _defaultAssetTransformerRegistrations.TryGetValue(className, out var count);
        _defaultAssetTransformerRegistrations[className] = count + 1;
        if (count == 0 && (_flags & ProjectFlag.ReadOnly) == 0 && File.Exists(_defaultAssetPipeline))
            UpdateDefaultAssetPipeline();
        return new DefaultAssetTransformerRegistration(this, className);
    }

    public void SetFocusedTab(EditorTab? tab)
    {
        if (!ReferenceEquals(FocusedTab, tab))
        {
            _focusedTab = tab is null ? null : new WeakReference<EditorTab>(tab);
            tab?.RaiseFocused();
        }

        var ownerTab = tab?.GetOwnerTab();
        if (!ReferenceEquals(RootFocusedTab, ownerTab))
            _focusedRootTab = ownerTab is null ? null : new WeakReference<EditorTab>(ownerTab);
    }

    public EditorTab? RootFocusedTab => GetWeakTarget(_focusedRootTab);

    public static void SetMonoFont(ImFont? font) => _monoFont = font;

    public static ImFont? GetMonoFont() => _monoFont;

    private EditorTab? FocusedTab => GetWeakTarget(_focusedTab);

    private static Regex PatternToRegex(string pattern)
    {
        var result = new StringBuilder("^(?:");
        foreach (var character in pattern)
        {
            if (character == '*')
                result.Append(".*");
            else if (character == '?')
                result.Append('.');
            else
                result.Append(Regex.Escape(character.ToString()));
        }
        result.Append(")$");
        return new Regex(result.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    }

    private static void CreateAssetPipeline(string fileName)
    {
        using var jsonFile = new JSONFile();
        jsonFile.SaveFile(new FileIdentifier(fileName));
    }

    private static (string Name, string Args) ParseCommand(string command)
    {
        var spacePosition = command.IndexOf(' ');
        if (spacePosition != -1)
            return (command[..spacePosition].Trim(), command[(spacePosition + 1)..].Trim());
        return (command.Trim(), string.Empty);
    }

    private void UpdateDefaultAssetPipeline()
    {
        using var assetPipeline = new AssetPipeline();
        if (!assetPipeline.LoadFile(new FileIdentifier(_defaultAssetPipeline)))
            return;

        IEnumerable<string> defaultTransformers =
        [
            typeof(ModelImporter).GetClassName(),
            typeof(CalculateAnimationVelocityTransformer).GetClassName(),
            typeof(GenerateWorldSpaceTracksTransformer).GetClassName(),
            .. _defaultAssetTransformerRegistrations.Keys.Order(StringComparer.Ordinal),
        ];

        var isUpdated = false;
        foreach (var className in defaultTransformers)
        {
            var isFound = false;
            foreach (var transformer in assetPipeline.GetTransformers())
            {
                if (transformer is not null && string.Equals(transformer.GetTypeName(), className, StringComparison.Ordinal))
                {
                    isFound = true;
                    break;
                }
            }
            if (isFound)
                continue;

            if (Context.CreateObject(className) is AssetTransformer createdTransformer)
            {
                assetPipeline.AddTransformer(createdTransformer);
                isUpdated = true;
            }
        }

        if (isUpdated)
            assetPipeline.SaveFile(new FileIdentifier(_defaultAssetPipeline));
    }

    private void SerializeProject(Archive archive)
    {
        using var block = archive.OpenUnorderedBlock("Project");
        SerializeInBlock(archive);
    }

    private void InitializeHotkeys()
    {
        _hotkeyManager.BindHotkey(this, HotkeySaveProject, Save);
    }

    private void EnsureDirectoryInitialized()
    {
        EnsureDirectory(_cachePath);
        EnsureDirectory(_tempPath);
        EnsureDirectory(_artifactsPath);

        if (!File.Exists(_settingsJsonPath))
        {
            if (Directory.Exists(_settingsJsonPath))
                Directory.Delete(_settingsJsonPath, true);
            File.WriteAllText(_settingsJsonPath, "{}");
        }

        if (!File.Exists(_projectJsonPath))
        {
            if (Directory.Exists(_projectJsonPath))
                Directory.Delete(_projectJsonPath, true);
            File.WriteAllText(_projectJsonPath, "{}");
            _firstInitialization = true;
        }

        if (!File.Exists(_cacheJsonPath))
        {
            if (Directory.Exists(_cacheJsonPath))
                Directory.Delete(_cacheJsonPath, true);
            File.WriteAllText(_cacheJsonPath, "{}");
        }

        // Legacy: to support old projects
        if (Directory.Exists(_projectPath + "Resources/"))
            _dataPath = _projectPath + "Resources/";

        EnsureDirectory(_dataPath);

        if (!File.Exists(_uiIniPath))
        {
            if (Directory.Exists(_uiIniPath))
                Directory.Delete(_uiIniPath, true);
            _pendingResetLayout = true;
        }

        _defaultAssetPipeline = _dataPath + "Default.assetpipeline";
    }

    private void InitializeDefaultProject()
    {
        // TODO: port reloadable user plugins.
        // pluginManager_->SetPluginsLoaded({SceneViewerApplication::GetStaticPluginName()});

        const string configName = "View Current Scene";
        const string sceneViewerPluginName = "Builtin.SceneViewer";
        using (var configuration = new LaunchConfiguration(configName, sceneViewerPluginName))
            _launchManager.AddConfiguration(configuration);
        _currentLaunchConfiguration = configName;

        const string defaultSceneName = "Scenes/Default.scene";
        var parameters = new DefaultSceneParameters
        {
            HighQuality = true,
            CreateObjects = true,
        };
        DefaultSceneFunctions.CreateDefaultScene(_dataPath + defaultSceneName, parameters);

        ProcessRequest(new OpenResourceRequest(defaultSceneName));

        CreateAssetPipeline(_defaultAssetPipeline);

        Save();
    }

    private void InitializeResourceCache()
    {
        var engine = GetSubsystem<EngineSubsystem>();
        var cache = GetSubsystem<ResourceCache>();
        cache.ReleaseAllResources(true);

        var virtualFileSystem = GetSubsystem<VirtualFileSystem>();

        virtualFileSystem.SetWatching(false);

        virtualFileSystem.UnmountAll(false);
        virtualFileSystem.MountAliasRoot();
        virtualFileSystem.MountRoot();
        if (!string.IsNullOrEmpty(_oldCacheState.EditorData))
            virtualFileSystem.MountDir(_oldCacheState.EditorData);

        MountPoint? coreDataMountPoint = null;
        if (!string.IsNullOrEmpty(_oldCacheState.CoreData))
            coreDataMountPoint = virtualFileSystem.MountDir(_oldCacheState.CoreData);
        var cacheMountPoint = virtualFileSystem.MountDir(_cachePath);
        var dataMountPoint = virtualFileSystem.MountDir(_dataPath);
        if (coreDataMountPoint is not null)
            virtualFileSystem.MountAlias("res:CoreData", coreDataMountPoint);
        virtualFileSystem.MountAlias("res:Cache", cacheMountPoint);
        virtualFileSystem.MountAlias("res:Data", dataMountPoint);

        virtualFileSystem.MountDir("conf", engine.GetAppPreferencesDir());

        virtualFileSystem.SetWatching(true);
    }

    private void ResetLayout()
    {
        _pendingResetLayout = false;

        ImGui.DockBuilderRemoveNode(_dockspaceId);
        ImGui.DockBuilderAddNode(_dockspaceId);
        using (var viewport = ImGui.GetMainViewport())
            ImGui.DockBuilderSetNodeSize(_dockspaceId, viewport.Size);

        var dockCenter = _dockspaceId;
        var dockLeft = ImGui.DockBuilderSplitNode(
            dockCenter, (ImGuiDir)0, 0.20f, out _, out dockCenter);
        var dockRight = ImGui.DockBuilderSplitNode(
            dockCenter, (ImGuiDir)1, 0.30f, out _, out dockCenter);
        var dockBottom = ImGui.DockBuilderSplitNode(
            dockCenter, (ImGuiDir)3, 0.30f, out _, out dockCenter);

        foreach (var tab in _tabs)
        {
            switch (tab.Placement)
            {
                case EditorTabPlacement.DockCenter:
                    ImGui.DockBuilderDockWindow(tab.UniqueIdBytes, dockCenter);
                    break;
                case EditorTabPlacement.DockLeft:
                    ImGui.DockBuilderDockWindow(tab.UniqueIdBytes, dockLeft);
                    break;
                case EditorTabPlacement.DockRight:
                    ImGui.DockBuilderDockWindow(tab.UniqueIdBytes, dockRight);
                    break;
                case EditorTabPlacement.DockBottom:
                    ImGui.DockBuilderDockWindow(tab.UniqueIdBytes, dockBottom);
                    break;
            }
        }
        ImGui.DockBuilderFinish(_dockspaceId);

        foreach (var tab in _tabs)
        {
            if ((tab.Flags & EditorTabFlag.OpenByDefault) != 0)
                tab.Open();
        }
    }

    private void ApplyPlugins()
    {
        var editorPluginManager = GetSubsystem<EditorPluginManager>();
        editorPluginManager.Apply(this);

        foreach (var tab in _tabs)
            tab.ApplyPlugins();
    }

    private void SaveGitIgnore()
    {
        var content = new StringBuilder();

        content.Append("# Ignore asset cache\n");
        content.Append("/Cache/\n");
        content.Append("/Cache.json\n");
        content.Append('\n');

        content.Append("# Ignore temporary files\n");
        content.Append("/Temp/\n");
        content.Append('\n');

        content.Append("# Ignore UI settings\n");
        content.Append("/ui.ini\n");
        content.Append('\n');

        content.Append("# Ignore preview screenshot\n");
        content.Append("/Preview.png\n");
        content.Append('\n');

        content.Append("# Ignore artifacts\n");
        content.Append("/Artifacts/\n");
        content.Append('\n');

        content.Append("# Ignore internal files\n");
        foreach (var pattern in _ignoredFileNames)
            content.Append(pattern).Append('\n');
        content.Append('\n');

        try
        {
            File.WriteAllText(_gitIgnorePath, content.ToString());
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private void ProcessPendingRequests()
    {
        var requests = new List<PendingRequest>(_pendingRequests);
        _pendingRequests.Clear();

        foreach (var pending in requests)
        {
            EditorTab? sender = null;
            pending.Sender?.TryGetTarget(out sender);
            OnRequest?.Invoke(sender, pending.Request);
            pending.Request.InvokeProcessCallback();
        }
    }

    private void ProcessDelayedSaves(bool forceSave = false)
    {
        var cache = GetSubsystem<ResourceCache>();
        foreach (var pair in _delayedFileSaves)
        {
            var resourceName = pair.Key;
            var delayedSave = pair.Value;
            if (!forceSave && delayedSave.Timer.GetMSec(false) < _saveDelayMs)
                continue;

            var fileExists = File.Exists(delayedSave.FileName);

            if (delayedSave.Bytes is not null)
            {
                try
                {
                    File.WriteAllBytes(delayedSave.FileName, delayedSave.Bytes);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
            else
                delayedSave.Resource!.SaveFile(new FileIdentifier(delayedSave.FileName));

            var needReload = !fileExists;
            delayedSave.OnSaved?.Invoke(delayedSave.FileName, resourceName, ref needReload);

            if (!needReload)
                cache.IgnoreResourceReload(resourceName);

            delayedSave.Clear();
        }

        foreach (var resourceName in _delayedFileSaves
                     .Where(static pair => pair.Value.IsEmpty)
                     .Select(static pair => pair.Key)
                     .ToArray())
        {
            _delayedFileSaves.Remove(resourceName);
        }
    }

    private void ProcessCommand(string command, bool exitOnCompletion)
    {
        var (name, args) = ParseCommand(command);

        if (!string.Equals(name, "Idle", StringComparison.Ordinal))
        {
            var processed = false;
            OnCommand?.Invoke(this, name, args, ref processed);

            if (!processed)
                Log.GetLogger().LogWarning("Cannot process command: {Command}", command);
        }

        if (exitOnCompletion)
        {
            _closeProjectResult = CloseProjectResult.Closed;
            SendEvent<RedBolt.Input.InputEvents.ExitRequestedEvent>();
        }
    }

    private void ProcessPendingRemoteCommands()
    {
        foreach (var command in _pendingRemoteCommands)
        {
            if (command.Result is { IsCompletedSuccessfully: true })
            {
                var (success, output) = command.Result.Result;
                command.Callback?.Invoke(success, output);
                command.Callback = null;
            }
        }

        _pendingRemoteCommands.RemoveAll(static command => command.Callback is null);
    }

    private void RenderAssetsToolbar()
    {
        var progressInfo = _assetManager.Progress;

        if (progressInfo.NumAssetsTotal == 0)
            return;

        Widgets.ToolbarSeparator();
        var ratio = (float)progressInfo.NumProcessedAssets / progressInfo.NumAssetsTotal;
        _assetsProgressText.FormatInPlace(
            $"Assets cooked {progressInfo.NumProcessedAssets}/{progressInfo.NumAssetsTotal}");

        // Show some small progress from the start for better visibility
        var progress = 0.05f + (1.0f - 0.05f) * ratio;
        ImGui.ProgressBar(progress, new Vector2f(200.0f, 0.0f), _assetsProgressText.ContentBytes);
    }

    private void RenderPluginReloadToolbar()
    {
        if (!_pluginManager.IsReloadPending && !_pluginManager.EnumerateAvailablePlugins().Any())
            return;

        Widgets.ToolbarSeparator();
        if (Widgets.ToolbarButton("\uf021"u8, "Reload plugins"u8))
            _pluginManager.Reload();
        if (_pluginManager.GetReloadBlockedReason() is { } reason)
        {
            ImGui.SameLine();
            _assetsProgressText.Set(reason);
            ImGui.TextUnformatted(_assetsProgressText.ContentBytes);
        }
    }

    private void RenderSavePendingToolbar()
    {
        if (_delayedFileSaves.Count == 0)
            return;

        Widgets.ToolbarSeparator();
        ImGui.Text("\uf0c7 Save in progress..."u8);
    }

    private static void EnsureDirectory(string path)
    {
        if (Directory.Exists(path))
            return;
        if (File.Exists(path))
            File.Delete(path);
        Directory.CreateDirectory(path);
    }

    private static string AddTrailingSlash(string path)
    {
        if (path.Length == 0 || path[^1] == '/')
            return path;
        return path + "/";
    }

    private static T? GetWeakTarget<T>(WeakReference<T>? reference)
        where T : class
    {
        return reference is not null && reference.TryGetTarget(out var target) ? target : null;
    }

    private sealed class PendingRequest
    {
        public PendingRequest(ProjectRequest request, EditorTab? sender)
        {
            Request = request;
            Sender = sender is null ? null : new WeakReference<EditorTab>(sender);
        }

        public ProjectRequest Request;
        public WeakReference<EditorTab>? Sender;
    }

    private sealed class PendingFileSave
    {
        public PendingFileSave(string fileName, byte[]? bytes, FileSavedCallback? onSaved, ResourceType? resource)
        {
            FileName = fileName;
            Bytes = bytes;
            OnSaved = onSaved;
            Resource = resource;
        }

        public void Clear()
        {
            Bytes = null;
            Resource = null;
        }

        public bool IsEmpty => Bytes is null && Resource is null;

        public string FileName;
        public byte[]? Bytes;
        public FileSavedCallback? OnSaved;
        public ResourceType? Resource;
        public readonly Timer Timer = new();
    }

    private sealed class PendingRemoteCommand
    {
        public CommandExecutedCallback? Callback;
        public Task<(bool Success, string Output)> Result = null!;
    }

    private sealed class DefaultAssetTransformerRegistration(Project owner, string className) : IDisposable
    {
        private Project? _owner = owner;

        public void Dispose()
        {
            if (_owner is not { } project)
                return;
            if (project._defaultAssetTransformerRegistrations.TryGetValue(className, out var count))
            {
                if (count > 1)
                    project._defaultAssetTransformerRegistrations[className] = count - 1;
                else
                    project._defaultAssetTransformerRegistrations.Remove(className);
            }
            _owner = null;
        }
    }

    private readonly bool _isHeadless;
    private readonly ProjectFlag _flags;
    private readonly bool _isXR;
    private readonly int _saveDelayMs = 3000;

    private readonly string _projectPath;

    private readonly string _cachePath;
    private readonly string _tempPath;
    private readonly string _artifactsPath;

    private readonly string _projectJsonPath;
    private readonly string _settingsJsonPath;
    private readonly string _cacheJsonPath;
    private readonly string _uiIniPath;
    private readonly string _gitIgnorePath;
    private readonly string _previewPngPath;

    private string _dataPath;
    private string _defaultAssetPipeline = string.Empty;

    private readonly CString _uiIniPathCString;
    private readonly ResourceCacheGuard _oldCacheState;

    private readonly AssetManager _assetManager;
    private readonly HotkeyManager _hotkeyManager;
    private readonly SettingsManager _settingsManager;
    private readonly UndoManager _undoManager;
    private readonly PluginManager _pluginManager;
    private readonly PluginManager _defaultPluginManager;
    private readonly LaunchManager _launchManager;
    private readonly ToolManager _toolManager;

    private bool _assetManagerInitialized;
    private bool _initializationPending;
    private bool _firstInitialization;
    private bool _initialized;
    private bool _hasUnsavedChanges;
    private readonly List<EditorTab> _tabs = [];
    private readonly SortedDictionary<string, EditorTab> _sortedTabs = new(StringComparer.Ordinal);
    private readonly SortedSet<string> _ignoredFileNames = new(StringComparer.Ordinal);
    private readonly List<Regex> _ignoredFileNameRegexes = [];
    private readonly List<AnalyzeFileCallback> _analyzeFileCallbacks = [];
    private readonly Dictionary<string, int> _defaultAssetTransformerRegistrations = new(StringComparer.Ordinal);

    /// <summary>Commands to be executed after initialization.</summary>
    private readonly List<(string Command, bool ExitOnCompletion)> _pendingCommands = [];

    /// <summary>Global requests to be processed.</summary>
    private readonly List<PendingRequest> _pendingRequests = [];

    /// <summary>File save requests to be processed.</summary>
    private readonly Dictionary<string, PendingFileSave> _delayedFileSaves = new(StringComparer.Ordinal);

    /// <summary>Ongoing remote commands.</summary>
    private readonly List<PendingRemoteCommand> _pendingRemoteCommands = [];

    /// <summary>Close popup handling.</summary>
    private readonly CloseDialog _closeDialog;
    private CloseProjectResult _closeProjectResult;

    /// <summary>UI state.</summary>
    private bool _pendingResetLayout;
    private uint _dockspaceId;
    private WeakReference<EditorTab>? _focusedTab;
    private WeakReference<EditorTab>? _focusedRootTab;
    private bool _areGlobalHotkeysEnabled = true;
    private bool _isHighlightEnabled;
    private string _currentLaunchConfiguration = string.Empty;
    private DateTime? _pluginReloadEndTime;

    private readonly CString _assetsProgressText = new();
    private readonly CString _saveHotkeyLabel = new();
}
