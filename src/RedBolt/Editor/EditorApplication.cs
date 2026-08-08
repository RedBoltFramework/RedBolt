// Copyright (c) 2017-2023 the rbfx project.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Editor;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Microsoft.Extensions.Logging;

using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Editor.Assets;
using RedBolt.Editor.Core;
using RedBolt.Editor.Foundation;
using RedBolt.Editor.Foundation.Shared;
using RedBolt.Editor.Project;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.Platform;
using RedBolt.Resource;
using RedBolt.SystemUI;

using DearImGuiSubsystem = RedBolt.DearImGui.DearImGui;
using EditorConsole = RedBolt.SystemUI.Console;
using EditorProject = RedBolt.Editor.Project.Project;
using EngineLogLevel = RedBolt.Logging.LogLevel;
using File = global::System.IO.File;
using ImageResource = RedBolt.Resource.Image;
using RmlUISubsystem = RedBolt.RmlUI.RmlUI;

public unsafe class EditorApplication : Application
{
    private const uint ImGuiConfigFlagsDpiEnableScaleFonts = 1u << 14;

    public EditorApplication()
    {
        _editorPluginManager = new EditorPluginManager();

        _editorPluginManager.AddPlugin<EditorProject>("Assets.ModelImporter", static _ =>
        {
            if (!Context.Instance.IsReflected<ModelImporter>())
                Context.Instance.RegisterAssembly(typeof(ModelImporter).Assembly);
        });

        _editorPluginManager.AddPlugin<EditorProject>("Foundation.StandardFileTypes", StandardFileTypes.Register);
        _editorPluginManager.AddPlugin<EditorProject>(
            "Foundation.ConcurrentAssetProcessing", ConcurrentAssetProcessing.Register);

        _editorPluginManager.AddPlugin<EditorProject>(
            "Foundation.GameView", static project => project.AddTab(new GameViewTab()));
        _editorPluginManager.AddPlugin<EditorProject>("Foundation.SceneView", static project =>
        {
            project.AddTab(new SceneViewTab());
            if (!Context.Instance.IsReflected<SceneResourceForEditor>())
                Context.Instance.RegisterAssembly(typeof(SceneResourceForEditor).Assembly);
        });
        _editorPluginManager.AddPlugin<EditorProject>(
            "Foundation.Texture2DView", static project => project.AddTab(new Texture2DViewTab()));
        _editorPluginManager.AddPlugin<EditorProject>(
            "Foundation.TextureCubeView", static project => project.AddTab(new TextureCubeViewTab()));
        _editorPluginManager.AddPlugin<EditorProject>(
            "Foundation.ModelView", static project => project.AddTab(new ModelViewTab()));
        _editorPluginManager.AddPlugin<EditorProject>(
            "Foundation.AnimationView", static project => project.AddTab(new AnimationViewTab()));
        _editorPluginManager.AddPlugin<EditorProject>(
            "Foundation.Console", static project => project.AddTab(new ConsoleTab()));
        _editorPluginManager.AddPlugin<EditorProject>(
            "Foundation.ResourceBrowser", static project => project.AddTab(new ResourceBrowserTab()));
        _editorPluginManager.AddPlugin<EditorProject>(
            "Foundation.HierarchyBrowser", static project => project.AddTab(new HierarchyBrowserTab()));
        _editorPluginManager.AddPlugin<EditorProject>(
            "Foundation.Settings", static project => project.AddTab(new SettingsTab()));
        _editorPluginManager.AddPlugin<EditorProject>(
            "Foundation.Inspector", static project => project.AddTab(new InspectorTab()));

        _editorPluginManager.AddPlugin<SettingsTab>("Foundation.Settings.KeyBindings", static settingsTab =>
            settingsTab.GetProject().SettingsManager.AddPage(new KeyBindingsPage()));
        _editorPluginManager.AddPlugin<SettingsTab>("Foundation.Settings.Launch", static settingsTab =>
            settingsTab.GetProject().SettingsManager.AddPage(new LaunchPage()));
        // TODO: port Foundation_PluginsPage with reloadable user plugins.

        _editorPluginManager.AddPlugin<SceneViewTab>(
            "Foundation.SceneView.CreatePrefabFromNode", static sceneViewTab =>
                sceneViewTab.RegisterAddon(new CreatePrefabFromNode(sceneViewTab)));
        _editorPluginManager.AddPlugin<SceneViewTab>("Foundation.SceneView.EditorCamera", static sceneViewTab =>
        {
            var settingsPage = new CameraController.SettingsPage();
            sceneViewTab.GetProject().SettingsManager.AddPage(settingsPage);
            sceneViewTab.RegisterAddon(new EditorCamera(sceneViewTab, settingsPage));
        });
        _editorPluginManager.AddPlugin<SceneViewTab>(
            "Foundation.SceneView.Selector", static sceneViewTab =>
                sceneViewTab.RegisterAddon(new SceneSelector(sceneViewTab)));
        _editorPluginManager.AddPlugin<SceneViewTab>(
            "Foundation.SceneView.Hierarchy", static sceneViewTab =>
                sceneViewTab.RegisterAddon(new SceneHierarchy(sceneViewTab)));
        _editorPluginManager.AddPlugin<SceneViewTab>(
            "Foundation.SceneView.SelectionRenderer", static sceneViewTab =>
        {
            var settingsPage = new SceneSelectionRenderer.SettingsPage();
            sceneViewTab.GetProject().SettingsManager.AddPage(settingsPage);
            sceneViewTab.RegisterAddon(new SceneSelectionRenderer(sceneViewTab, settingsPage));
        });
        _editorPluginManager.AddPlugin<SceneViewTab>(
            "Foundation.SceneView.TransformGizmo", static sceneViewTab =>
        {
            var settingsPage = new TransformManipulator.SettingsPage();
            sceneViewTab.GetProject().SettingsManager.AddPage(settingsPage);
            sceneViewTab.RegisterAddon(new TransformManipulator(sceneViewTab, settingsPage));
        });
        _editorPluginManager.AddPlugin<SceneViewTab>(
            "Foundation.SceneView.DragAndDropPrefab", static sceneViewTab =>
                sceneViewTab.RegisterAddon(new SceneDragAndDropPrefab(sceneViewTab)));
        _editorPluginManager.AddPlugin<SceneViewTab>(
            "Foundation.SceneView.DragAndDropMaterial", static sceneViewTab =>
                sceneViewTab.RegisterAddon(new SceneDragAndDropMaterial(sceneViewTab)));
        _editorPluginManager.AddPlugin<SceneViewTab>(
            "Foundation.SceneView.DragAndDropAnimation", static sceneViewTab =>
                sceneViewTab.RegisterAddon(new SceneDragAndDropAnimation(sceneViewTab)));
        _editorPluginManager.AddPlugin<SceneViewTab>("Foundation.SceneView.DebugInfo", static sceneViewTab =>
        {
            var settingsPage = new SceneDebugInfo.SettingsPage();
            sceneViewTab.GetProject().SettingsManager.AddPage(settingsPage);
            sceneViewTab.RegisterAddon(new SceneDebugInfo(sceneViewTab, settingsPage));
        });
        _editorPluginManager.AddPlugin<SceneViewTab>(
            "Foundation.SceneView.Screenshot", static sceneViewTab =>
                sceneViewTab.RegisterAddon(new SceneScreenshot(sceneViewTab)));

        _editorPluginManager.AddPlugin<InspectorTab>("Foundation.Inspector.Empty", static inspectorTab =>
            inspectorTab.RegisterAddon(new EmptyInspector(inspectorTab.GetProject())));
        _editorPluginManager.AddPlugin<InspectorTab>(
            "Foundation.Inspector.AssetPipeline", static inspectorTab =>
                inspectorTab.RegisterAddon(new AssetPipelineInspector(inspectorTab.GetProject())));
        _editorPluginManager.AddPlugin<InspectorTab>("Foundation.Inspector.Animation", static inspectorTab =>
            inspectorTab.RegisterAddon(new AnimationInspector(inspectorTab.GetProject())));
        _editorPluginManager.AddPlugin<InspectorTab>("Foundation.Inspector.Texture2D", static inspectorTab =>
            inspectorTab.RegisterAddon(new Texture2DInspector(inspectorTab.GetProject())));
        _editorPluginManager.AddPlugin<InspectorTab>("Foundation.Inspector.TextureCube", static inspectorTab =>
            inspectorTab.RegisterAddon(new TextureCubeInspector(inspectorTab.GetProject())));
        _editorPluginManager.AddPlugin<InspectorTab>("Foundation.Inspector.Model", static inspectorTab =>
            inspectorTab.RegisterAddon(new ModelInspector(inspectorTab.GetProject())));
        _editorPluginManager.AddPlugin<InspectorTab>("Foundation.Inspector.Prefab", static inspectorTab =>
            inspectorTab.RegisterAddon(new PrefabInspector(inspectorTab.GetProject())));
        _editorPluginManager.AddPlugin<InspectorTab>("Foundation.Inspector.Material", static inspectorTab =>
            inspectorTab.RegisterAddon(new MaterialInspector(inspectorTab.GetProject())));
        _editorPluginManager.AddPlugin<InspectorTab>(
            "Foundation.Inspector.NodeComponent", static inspectorTab =>
                inspectorTab.RegisterAddon(new NodeComponentInspector(inspectorTab.GetProject())));
        _editorPluginManager.AddPlugin<InspectorTab>(
            "Foundation.Inspector.PlaceholderResource", static inspectorTab =>
                inspectorTab.RegisterAddon(new PlaceholderResourceInspector(inspectorTab.GetProject())));
        _editorPluginManager.AddPlugin<InspectorTab>("Foundation.Inspector.RenderPath", static inspectorTab =>
            inspectorTab.RegisterAddon(new RenderPathInspector(inspectorTab.GetProject())));
        _editorPluginManager.AddPlugin<InspectorTab>(
            "Foundation.Inspector.SerializableResource", static inspectorTab =>
                inspectorTab.RegisterAddon(new SerializableResourceInspector(inspectorTab.GetProject())));
        _editorPluginManager.AddPlugin<InspectorTab>("Foundation.Inspector.Sound", static inspectorTab =>
            inspectorTab.RegisterAddon(new SoundInspector(inspectorTab.GetProject())));

        _editorPluginManager.AddPlugin<ResourceBrowserTab>(
            "Foundation.ResourceBrowser.AssetPipelineFactory", static resourceBrowserTab =>
                resourceBrowserTab.AddFactory(new AssetPipelineFactory()));
        _editorPluginManager.AddPlugin<ResourceBrowserTab>(
            "Foundation.ResourceBrowser.MaterialFactory", static resourceBrowserTab =>
                resourceBrowserTab.AddFactory(new MaterialFactory()));
        _editorPluginManager.AddPlugin<ResourceBrowserTab>(
            "Foundation.ResourceBrowser.SceneFactory", static resourceBrowserTab =>
        {
            resourceBrowserTab.AddFactory(new SceneFactory(true));
            resourceBrowserTab.AddFactory(new SceneFactory(false));
        });

        // TODO: port Foundation_ProjectGlue with reloadable user plugins.
        _editorPluginManager.AddPlugin<ResourceBrowserTab>(
            "Foundation.Glue.ResourceBrowser", static _ => { });
        _editorPluginManager.AddPlugin<SceneViewTab>("Foundation.Glue.SceneView", static sceneViewTab =>
        {
            var hierarchyBrowserTab = sceneViewTab.GetProject().FindTab<HierarchyBrowserTab>();
            var sceneHierarchy = sceneViewTab.GetAddon<SceneHierarchy>();
            if (hierarchyBrowserTab is null || sceneHierarchy is null)
                return;

            var hierarchyBrowserTabWeak = new WeakReference<HierarchyBrowserTab>(hierarchyBrowserTab);
            var sceneHierarchyWeak = new WeakReference<SceneHierarchy>(sceneHierarchy);
            sceneViewTab.OnFocused += _ =>
            {
                if (hierarchyBrowserTabWeak.TryGetTarget(out var hierarchyBrowserTabTarget)
                    && sceneHierarchyWeak.TryGetTarget(out var sceneHierarchyTarget))
                {
                    hierarchyBrowserTabTarget.ConnectToSource(sceneHierarchyTarget);
                }
            };
        });
    }

    public override void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        foreach (var texture in _projectPreviews.Values)
            texture?.Dispose();
        _projectPreviews.Clear();
        foreach (var labels in _recentProjectLabels.Values)
            labels.Dispose();
        _recentProjectLabels.Clear();
        _projectPreviewTextureReference.Dispose();
        _editorPluginManager.Dispose();

        using (global::Std.Infrastructure.ProviderRegistry.PushModule("RedBolt.Bindings.RmlUi"))
            base.Dispose();

        if (_settingsHandlerTypeName != 0)
        {
            Marshal.FreeCoTaskMem(_settingsHandlerTypeName);
            _settingsHandlerTypeName = 0;
        }
    }

    public override void SerializeInBlock(Archive archive)
    {
        ArgumentNullException.ThrowIfNull(archive);

        if (!archive.IsInput || !archive.IsUnorderedAccessSupportedInCurrentBlock
            || archive.HasElementOrBlock("RecentProjects"))
        {
            ArchiveSerialization.SerializeStringVector(archive, "RecentProjects", _recentProjects);
        }

        if (archive.IsInput)
            _recentProjects.RemoveAll(static path => string.IsNullOrEmpty(path) || !Directory.Exists(path));
    }

    protected override void Setup(EngineParameters parameters)
    {
        Context.RegisterSubsystem(_editorPluginManager);

        // Required until SDL supports hdpi on Windows.
        // The managed SDL backend configures process DPI awareness during platform initialization.

        GetSubsystem<Log>().SetLogFormat("[%H:%M:%S] [%l] [%n] : %v");
        MathDefs.SetRandomSeed(Time.GetTimeSinceEpoch());

        parameters.WindowTitle = GetTypeName();
        parameters.ApplicationName = GetWindowTitle();
        parameters.Headless = false;
        parameters.FullScreen = false;
        parameters.Borderless = false;
        parameters.LogLevel = EngineLogLevel.Debug;
        parameters.WindowResizable = true;
        parameters.AutoloadPaths = string.Empty;
        parameters.ResourcePaths = "CoreData;EditorData";
        parameters.ResourceRootFile = string.Empty;
        parameters.WindowMaximize = true;
        parameters.EngineAutoLoadScripts = false;
        // TODO: port user plugin reload: engineParameters_[EP_RELOAD_PLUGINS] = true;
        parameters.ReloadPlugins = false;
        parameters.Profile = true;

        // Use detected prefix path as default. Could be overridden with command line.
        var prefixPath = FindResourcePrefixPath();
        if (prefixPath.Length != 0)
            parameters.ResourcePrefixPaths = prefixPath;

        // TODO: Consider scaling fonts based on DPI. ImGuiConfigFlags_DpiEnableScaleFonts seems to create issues on Retina.
        var imguiFlags = (ImGuiConfigFlags)ImGuiConfigFlagsDpiEnableScaleFonts;
        imguiFlags |= ImGuiConfigFlags.ImGuiConfigFlagsViewportsEnable;

        if (OperatingSystem.IsLinux()
            && string.Equals(Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"), "wayland", StringComparison.Ordinal))
        {
            imguiFlags &= ~ImGuiConfigFlags.ImGuiConfigFlagsViewportsEnable;
        }

        parameters.SystemUIFlags = (uint)imguiFlags;
    }

    protected override void SetupCommandLine(CommandLineParser parser, EngineParameters parameters)
    {
        parser.AddFlag(["--read-only"],
            "Prevents Editor from modifying any project files, unless it is explicitly requested via user command.",
            () => _readOnly = true);
        parser.AddFlag(["--single-process"],
            "Prevents Editor from spawning additional processes, unless it is explicitly requested via user command.",
            () => _singleProcess = true);
        parser.AddOption(["--command"], "command", "Command to execute on startup.", value => _command = value);
        parser.AddFlag(["--exit"], "Forces Editor to exit after command execution.", () => _exitAfterCommand = true);
        parser.AddPositional("project", "Project to open or create on startup.", value => _pendingOpenProject = value);
    }

    protected override void Start()
    {
        // TODO: port NFD_Init() and NFD_GetError().

        var input = GetSubsystem<RedBolt.Input.Input>();
        var virtualFileSystem = GetSubsystem<VirtualFileSystem>();
        var isHeadless = Engine.IsHeadless;

        _tempJsonPath = Engine.GetAppPreferencesDir() + "Temp.json";
        _settingsJsonPath = Engine.GetAppPreferencesDir() + "Settings.json";

        using (var tempFile = new JSONFile())
        {
            if (tempFile.LoadFile(new FileIdentifier(_tempJsonPath)))
                tempFile.LoadObject(this);
        }

        input.SetMouseMode(RedBolt.Input.MouseMode.Absolute);
        input.SetMouseVisible(true);
        input.SetEnabled(false);

        virtualFileSystem.SetWatching(true);
        Engine.SetAutoExit(false);

        // Creates console but makes sure its UI is not rendered. Console rendering is done manually in editor.
        var console = Context.GetSubsystem<EditorConsole>();
        if (console is null)
        {
            console = new EditorConsole();
            Context.RegisterSubsystem(console);
        }
        console.SetAutoVisibleOnError(false);

        // Managed Console only dispatches commands to registered event receivers and never executes shell commands.

        // Hud will be rendered manually.
        var debugHud = Context.GetSubsystem<DebugHud>();
        if (debugHud is null)
        {
            debugHud = new DebugHud();
            Context.RegisterSubsystem(debugHud);
        }
        debugHud.SetMode(DebugHudMode.None);

        if (Context.GetSubsystem<RmlUISubsystem>() is null)
        {
            using var providerModule = global::Std.Infrastructure.ProviderRegistry.PushModule("RedBolt.Bindings.RmlUi");
            Context.RegisterSubsystem(new RmlUISubsystem());
        }

        SubscribeToEvent<CoreEvents.UpdateEvent>((ref CoreEvents.UpdateEvent _) => Render());
        SubscribeToEvent<CoreEvents.EndFrameEvent>((ref CoreEvents.EndFrameEvent _) => UpdateProjectStatus());
        SubscribeToEvent<RedBolt.Input.InputEvents.ExitRequestedEvent>(
            (ref RedBolt.Input.InputEvents.ExitRequestedEvent _) => OnExitRequested());
        SubscribeToEvent<EngineEvents.ConsoleUriClickEvent>(OnConsoleUriClick);

        if (!isHeadless)
        {
            Context.RegisterSubsystem(new DearImGuiSubsystem((ImGuiConfigFlags)Engine.Parameters.SystemUIFlags));
            InitializeUI();

            // Avoid creating imgui.ini if project with its own imgui.ini is about to be opened.
            if (_pendingOpenProject.Length != 0)
            {
                using var io = ImGui.GetIO();
                io.IniFilename = 0;
            }
        }

        if (_pendingOpenProject.Length != 0)
            OpenProject(_pendingOpenProject);
        else
            _command = string.Empty; // Execute commands only if the project is opened too.
    }

    protected override void Stop()
    {
        var workQueue = GetSubsystem<WorkQueue>();
        workQueue.CompleteAll();

        CloseProject();

        Context.RemoveSubsystem<WorkQueue>(); // Prevents deadlock when unloading plugin AppDomain in managed host.
        workQueue.Dispose();

        Context.RemoveSubsystem<EditorPluginManager>();

        // TODO: port NFD_Quit().
    }

    public void OpenProject(string projectPath)
    {
        _pendingOpenProject = AddTrailingSlash(projectPath);
    }

    public void CloseProject()
    {
        if (_project is null)
            return;

        var project = _project;
        project.Destroy();
        _project = null;
        Context.RemoveSubsystem<EditorProject>();
        project.Dispose();
    }

    protected void InitializeUI()
    {
        if (_uiAlreadyInitialized)
            RecreateSystemUI();

        InitializeSystemUI();
        InitializeImGuiConfig();
        InitializeImGuiStyle();
        InitializeImGuiHandlers();

        _uiAlreadyInitialized = true;
    }

    protected void RecreateSystemUI()
    {
        EditorProject.SetMonoFont(null);
        var oldSystemUI = GetSubsystem<DearImGuiSubsystem>();
        Context.RemoveSubsystem<DearImGuiSubsystem>();
        oldSystemUI.Dispose();
        Context.RegisterSubsystem(new DearImGuiSubsystem((ImGuiConfigFlags)Engine.Parameters.SystemUIFlags));
    }

    protected void InitializeSystemUI()
    {
        ReadOnlySpan<ushort> fontAwesomeIconRanges = [0x0021, 0xf8ff, 0];
        ReadOnlySpan<ushort> notoSansRanges = [0x20, 0x52f, 0x1ab0, 0x2189, 0x2c60, 0x2e44, 0xa640, 0xab65, 0];
        ReadOnlySpan<ushort> notoMonoRanges = [0x20, 0x513, 0x1e00, 0x1f4d, 0];

        var systemUI = GetSubsystem<DearImGuiSubsystem>();
        systemUI.ApplyStyleDefault(true, 1.0f);

        var defaultFont = systemUI.AddFont("Fonts/NotoSans-Regular.ttf", notoSansRanges, 16.0f);
        systemUI.AddFont("Fonts/fa-solid-900.ttf", fontAwesomeIconRanges, 14.0f, true);
        systemUI.AddFont("Fonts/fa-solid-900.ttf", fontAwesomeIconRanges, 12.0f, true);

        using var io = ImGui.GetIO();
        io.FontDefault = defaultFont!;
        io.ConfigWindowsMoveFromTitleBarOnly = true;

        var monoFont = systemUI.AddFont("Fonts/NotoMono-Regular.ttf", notoMonoRanges, 14.0f);
        EditorProject.SetMonoFont(monoFont);
    }

    protected void InitializeImGuiConfig()
    {
        // Disable imgui saving UI settings on its own. These should be serialized to project file.
        using var io = ImGui.GetIO();
        io.ConfigViewportsNoAutoMerge = true;
        io.ConfigDpiScaleFonts = true;
        io.IniFilename = 0;
        io.ConfigFlags |= ImGuiConfigFlags.ImGuiConfigFlagsDockingEnable
            | ImGuiConfigFlags.ImGuiConfigFlagsNavEnableKeyboard;
        io.BackendFlags |= ImGuiBackendFlags.ImGuiBackendFlagsHasMouseCursors;
        io.ConfigWindowsResizeFromEdges = true;
    }

    protected void InitializeImGuiStyle()
    {
        using var style = ImGui.GetStyle();

        // TODO: Make configurable.
        style.WindowRounding = 3.0f;
        style.FrameBorderSize = 0.0f;
        style.WindowBorderSize = 1.0f;
        style.ItemSpacing = new Vector2f(4.0f, 4.0f);
        using var colors = style.Colors;
        colors[(int)ImGuiCol.ImGuiColText] = new Vector4f(1.00f, 1.00f, 1.00f, 1.00f);
        colors[(int)ImGuiCol.ImGuiColTextDisabled] = new Vector4f(0.50f, 0.50f, 0.50f, 1.00f);
        colors[(int)ImGuiCol.ImGuiColWindowBg] = new Vector4f(0.16f, 0.16f, 0.16f, 1.00f);
        colors[(int)ImGuiCol.ImGuiColChildBg] = new Vector4f(0.16f, 0.16f, 0.16f, 1.00f);
        colors[(int)ImGuiCol.ImGuiColPopupBg] = new Vector4f(0.14f, 0.14f, 0.14f, 1.00f);
        colors[(int)ImGuiCol.ImGuiColBorder] = new Vector4f(0.24f, 0.24f, 0.24f, 1.00f);
        colors[(int)ImGuiCol.ImGuiColBorderShadow] = new Vector4f(0.00f, 0.00f, 0.00f, 0.00f);
        colors[(int)ImGuiCol.ImGuiColFrameBg] = new Vector4f(0.26f, 0.26f, 0.26f, 1.00f);
        colors[(int)ImGuiCol.ImGuiColFrameBgHovered] = new Vector4f(0.32f, 0.32f, 0.32f, 1.00f);
        colors[(int)ImGuiCol.ImGuiColFrameBgActive] = new Vector4f(0.37f, 0.37f, 0.37f, 1.00f);
        colors[(int)ImGuiCol.ImGuiColTitleBg] = new Vector4f(0.12f, 0.12f, 0.12f, 1.00f);
        colors[(int)ImGuiCol.ImGuiColTitleBgActive] = new Vector4f(0.16f, 0.16f, 0.16f, 1.00f);
        colors[(int)ImGuiCol.ImGuiColTitleBgCollapsed] = new Vector4f(0.16f, 0.16f, 0.16f, 1.00f);
        colors[(int)ImGuiCol.ImGuiColMenuBarBg] = new Vector4f(0.14f, 0.14f, 0.14f, 1.00f);
        colors[(int)ImGuiCol.ImGuiColScrollbarBg] = new Vector4f(0.02f, 0.02f, 0.02f, 0.00f);
        colors[(int)ImGuiCol.ImGuiColScrollbarGrab] = new Vector4f(0.31f, 0.31f, 0.31f, 1.00f);
        colors[(int)ImGuiCol.ImGuiColScrollbarGrabHovered] = new Vector4f(0.41f, 0.41f, 0.41f, 1.00f);
        colors[(int)ImGuiCol.ImGuiColScrollbarGrabActive] = new Vector4f(0.51f, 0.51f, 0.51f, 1.00f);
        colors[(int)ImGuiCol.ImGuiColCheckMark] = new Vector4f(0.51f, 0.51f, 0.51f, 1.00f);
        colors[(int)ImGuiCol.ImGuiColSliderGrab] = new Vector4f(0.51f, 0.51f, 0.51f, 1.00f);
        colors[(int)ImGuiCol.ImGuiColSliderGrabActive] = new Vector4f(0.56f, 0.56f, 0.56f, 1.00f);
        colors[(int)ImGuiCol.ImGuiColButton] = new Vector4f(0.27f, 0.27f, 0.27f, 1.00f);
        colors[(int)ImGuiCol.ImGuiColButtonHovered] = new Vector4f(0.34f, 0.34f, 0.34f, 1.00f);
        colors[(int)ImGuiCol.ImGuiColButtonActive] = new Vector4f(0.38f, 0.38f, 0.38f, 1.00f);
        colors[(int)ImGuiCol.ImGuiColHeader] = new Vector4f(0.35f, 0.35f, 0.35f, 1.00f);
        colors[(int)ImGuiCol.ImGuiColHeaderHovered] = new Vector4f(0.39f, 0.39f, 0.39f, 1.00f);
        colors[(int)ImGuiCol.ImGuiColHeaderActive] = new Vector4f(0.44f, 0.44f, 0.44f, 1.00f);
        colors[(int)ImGuiCol.ImGuiColSeparator] = new Vector4f(0.24f, 0.24f, 0.24f, 1.00f);
        colors[(int)ImGuiCol.ImGuiColSeparatorHovered] = new Vector4f(0.31f, 0.31f, 0.31f, 1.00f);
        colors[(int)ImGuiCol.ImGuiColSeparatorActive] = new Vector4f(0.34f, 0.34f, 0.34f, 1.00f);
        colors[(int)ImGuiCol.ImGuiColResizeGrip] = new Vector4f(0.24f, 0.24f, 0.24f, 1.00f);
        colors[(int)ImGuiCol.ImGuiColResizeGripHovered] = new Vector4f(0.31f, 0.31f, 0.31f, 1.00f);
        colors[(int)ImGuiCol.ImGuiColResizeGripActive] = new Vector4f(0.37f, 0.37f, 0.37f, 1.00f);
        colors[(int)ImGuiCol.ImGuiColTab] = new Vector4f(0.26f, 0.26f, 0.26f, 0.40f);
        colors[(int)ImGuiCol.ImGuiColTabHovered] = new Vector4f(0.31f, 0.31f, 0.31f, 1.00f);
        colors[(int)ImGuiCol.ImGuiColTabSelected] = new Vector4f(0.28f, 0.28f, 0.28f, 1.00f);
        colors[(int)ImGuiCol.ImGuiColTabDimmed] = new Vector4f(0.17f, 0.17f, 0.17f, 1.00f);
        colors[(int)ImGuiCol.ImGuiColTabDimmedSelected] = new Vector4f(0.26f, 0.26f, 0.26f, 1.00f);
        colors[(int)ImGuiCol.ImGuiColDockingPreview] = new Vector4f(0.55f, 0.55f, 0.55f, 1.00f);
        colors[(int)ImGuiCol.ImGuiColDockingEmptyBg] = new Vector4f(0.20f, 0.20f, 0.20f, 1.00f);
        colors[(int)ImGuiCol.ImGuiColPlotLines] = new Vector4f(0.61f, 0.61f, 0.61f, 1.00f);
        colors[(int)ImGuiCol.ImGuiColPlotLinesHovered] = new Vector4f(1.00f, 0.43f, 0.35f, 1.00f);
        colors[(int)ImGuiCol.ImGuiColPlotHistogram] = new Vector4f(0.90f, 0.70f, 0.00f, 1.00f);
        colors[(int)ImGuiCol.ImGuiColPlotHistogramHovered] = new Vector4f(1.00f, 0.60f, 0.00f, 1.00f);
        colors[(int)ImGuiCol.ImGuiColTextSelectedBg] = new Vector4f(0.26f, 0.59f, 0.98f, 0.35f);
        colors[(int)ImGuiCol.ImGuiColDragDropTarget] = new Vector4f(1.00f, 1.00f, 0.00f, 0.90f);
        colors[(int)ImGuiCol.ImGuiColNavCursor] = new Vector4f(0.78f, 0.88f, 1.00f, 1.00f);
        colors[(int)ImGuiCol.ImGuiColNavWindowingHighlight] = new Vector4f(1.00f, 1.00f, 1.00f, 0.70f);
        colors[(int)ImGuiCol.ImGuiColNavWindowingDimBg] = new Vector4f(0.80f, 0.80f, 0.80f, 0.20f);
        colors[(int)ImGuiCol.ImGuiColModalWindowDimBg] = new Vector4f(0.44f, 0.44f, 0.44f, 0.35f);
    }

    protected void InitializeImGuiHandlers()
    {
        _settingsHandlerTypeName = _settingsHandlerTypeName == 0
            ? Marshal.StringToCoTaskMemUTF8("Project")
            : _settingsHandlerTypeName;

        using var handler = new ImGuiSettingsHandler
        {
            TypeName = _settingsHandlerTypeName,
            TypeHash = ImGui.ImHashStr("Project"u8, seed: 0),
            ReadOpenFn = (nint)(delegate* unmanaged[Cdecl]<nint, nint, nint, nint>)&ReadOpen,
            ReadLineFn = (nint)(delegate* unmanaged[Cdecl]<nint, nint, nint, nint, void>)&ReadLine,
            WriteAllFn = (nint)(delegate* unmanaged[Cdecl]<nint, nint, nint, void>)&WriteAll,
        };
        ImGui.AddSettingsHandler(handler);
    }

    protected Texture2D? GetProjectPreview(string projectPath)
    {
        if (_projectPreviews.TryGetValue(projectPath, out var cachedTexture))
            return cachedTexture;

        Texture2D? texture = null;
        var previewFileName = AddTrailingSlash(projectPath) + "Preview.png";
        if (File.Exists(previewFileName))
        {
            using var image = new ImageResource();
            if (image.LoadFile(new FileIdentifier(previewFileName)))
            {
                texture = new Texture2D();
                texture.SetData(image);
            }
        }

        _projectPreviews[projectPath] = texture;
        return texture;
    }

    protected string GetWindowTitle()
    {
        var result = "Editor";

        if (Context.GetSubsystem<Graphics>() is { } graphics)
            result += " | " + graphics.GetApiName();

        if (_project is not null)
            result += " | " + _project.ProjectPath;

        return result;
    }

    protected void Render()
    {
        var isHeadless = Engine.IsHeadless;
        if (isHeadless)
        {
            // Exit immediately if requested.
            if (_exiting)
            {
                GetSubsystem<WorkQueue>().CompleteAll();
                Engine.Exit();
            }

            // In headless mode only run Project.Render which acts as main loop.
            _project?.Render();
            return;
        }

        var hasToolbar = _project is not null;
        var toolbarButtonHeight = Widgets.GetSmallButtonSize();
        float toolbarWindowPadding;
        Vector2f itemInnerSpacing;
        using (var style = ImGui.GetStyle())
        {
            toolbarWindowPadding = style.ItemSpacing.Y;
            itemInnerSpacing = style.ItemInnerSpacing;
        }
        var toolbarHeight = hasToolbar ? toolbarButtonHeight + 2.0f * toolbarWindowPadding : 0.0f;
        var toolbarEffectiveHeight = toolbarHeight + 1.0f;

        var flags = ImGuiWindowFlags.ImGuiWindowFlagsMenuBar | ImGuiWindowFlags.ImGuiWindowFlagsNoDocking;
        Vector2f viewportPosition;
        Vector2f viewportSize;
        uint viewportId;
        using (var viewport = ImGui.GetMainViewport())
        {
            viewportPosition = viewport.Pos;
            viewportSize = viewport.Size;
            viewportId = viewport.Id;
        }
        ImGui.SetNextWindowPos(viewportPosition + new Vector2f(0.0f, toolbarEffectiveHeight));
        ImGui.SetNextWindowSize(viewportSize - new Vector2f(0.0f, toolbarEffectiveHeight));
        ImGui.SetNextWindowViewport(viewportId);
        ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarWindowRounding, 0.0f);
        flags |= ImGuiWindowFlags.ImGuiWindowFlagsNoDecoration | ImGuiWindowFlags.ImGuiWindowFlagsNoMove;
        flags |= ImGuiWindowFlags.ImGuiWindowFlagsNoBringToFrontOnFocus | ImGuiWindowFlags.ImGuiWindowFlagsNoNavFocus;
        ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarWindowPadding, Vector2f.Zero);
        var dockSpaceOpen = true;
        ImGui.Begin("DockSpace"u8, ref dockSpaceOpen, flags);
        ImGui.PopStyleVar(); // TODO: Technically illegal, should go after End().

        RenderMenuBar();
        RenderAboutDialog();

        if (_project is not null)
        {
            _project.Render();
        }
        else
        {
            // Render start page.
            Rect rect;
            using (var window = ImGui.GetCurrentWindow())
                rect = window.ContentRegionRect;

            var tileSize = new Vector2f(200.0f, 200.0f);
            ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarItemSpacing, new Vector2f(10.0f, 10.0f));
            ImGui.SetCursorScreenPos(rect.Center - new Vector2f(tileSize.X * 1.5f + 10.0f, tileSize.Y * 1.5f + 10.0f));
            ImGui.BeginGroup();

            var index = 0;
            for (var row = 0; row < 3; ++row)
            {
                for (var col = 0; col < 3; ++col, ++index)
                {
                    // Last tile never shows a project.
                    if (_recentProjects.Count <= index || row == 2 && col == 2)
                    {
                        if (ImGui.Button("Open/Create Project"u8, tileSize))
                            OpenOrCreateProject();
                    }
                    else
                    {
                        var projectPath = _recentProjects[index];
                        var labels = GetRecentProjectLabels(projectPath);
                        if (GetProjectPreview(projectPath) is { } previewTexture)
                        {
                            if (Widgets.ImageButton(previewTexture, _projectPreviewTextureReference,
                                    tileSize - itemInnerSpacing * 2.0f))
                            {
                                OpenProject(projectPath);
                            }
                        }
                        else if (ImGui.Button(labels.Path.ContentBytes, tileSize))
                            OpenProject(projectPath);

                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip(labels.Path.ContentBytes);
                    }
                    ImGui.SameLine();
                }
                ImGui.NewLine();
            }

            ImGui.EndGroup();
            ImGui.PopStyleVar();
        }

        float menuBarHeight;
        using (var window = ImGui.GetCurrentWindow())
            menuBarHeight = window.MenuBarHeight;

        ImGui.End(); // DockSpace
        ImGui.PopStyleVar();

        // TODO(editor): Refactor this function.
        if (hasToolbar)
        {
            ImGui.SetNextWindowPos(new Vector2f(viewportPosition.X, viewportPosition.Y + menuBarHeight));
            ImGui.SetNextWindowSize(new Vector2f(viewportSize.X, toolbarHeight));
            ImGui.SetNextWindowViewport(viewportId);

            const ImGuiWindowFlags toolbarWindowFlags = ImGuiWindowFlags.ImGuiWindowFlagsNoDocking
                | ImGuiWindowFlags.ImGuiWindowFlagsNoDecoration | ImGuiWindowFlags.ImGuiWindowFlagsNoMove
                | ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings;
            ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarWindowBorderSize, 0.0f);
            ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarWindowPadding,
                new Vector2f(toolbarWindowPadding, toolbarWindowPadding));
            ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarWindowMinSize, Vector2f.Zero);
            var toolbarOpen = true;
            ImGui.Begin("Toolbar"u8, ref toolbarOpen, toolbarWindowFlags);
            _project?.RenderToolbar();
            ImGui.End();
            ImGui.PopStyleVar(3);
        }

        // Dialog for a warning when application is being closed with unsaved resources.
        if (_exiting)
        {
            var workQueue = GetSubsystem<WorkQueue>();
            if (!workQueue.IsCompleted())
            {
                _numIncompleteTasks ??= workQueue.GetNumIncomplete();
                var numProcessedTasks = _numIncompleteTasks.Value
                    - global::System.Math.Min(workQueue.GetNumIncomplete(), _numIncompleteTasks.Value);

                ImGui.OpenPopup("Completing Tasks"u8);
                if (ImGui.BeginPopupModal("Completing Tasks"u8, flags: ImGuiWindowFlags.ImGuiWindowFlagsNoDocking
                        | ImGuiWindowFlags.ImGuiWindowFlagsNoResize | ImGuiWindowFlags.ImGuiWindowFlagsNoMove
                        | ImGuiWindowFlags.ImGuiWindowFlagsPopup))
                {
                    ImGui.TextUnformatted("Some tasks are in progress and are being completed. Please wait."u8);
                    Vector2f? progressBarSize = null;
                    ImGui.ProgressBar(100.0f * numProcessedTasks / _numIncompleteTasks.Value, in progressBarSize);
                    ImGui.EndPopup();
                }
            }
            else if (_project is not null)
            {
                var result = _project.CloseGracefully();
                if (result == CloseProjectResult.Closed)
                    Engine.Exit();
                else if (result == CloseProjectResult.Canceled)
                    _exiting = false;
            }
            else
            {
                GetSubsystem<WorkQueue>().CompleteAll();
                Engine.Exit();
            }
        }

        if (!_exiting)
            _numIncompleteTasks = null;

        var title = GetWindowTitle();
        if (!string.Equals(_windowTitle, title, StringComparison.Ordinal))
        {
            GetSubsystem<Graphics>().SetWindowTitle(title);
            _windowTitle = title;
        }
    }

    protected void RenderMenuBar()
    {
        if (!ImGui.BeginMainMenuBar())
            return;

        if (ImGui.BeginMenu("Project"u8))
        {
            if (_project is not null)
            {
                _project.RenderProjectMenu();
                ImGui.Separator();
            }

            if (ImGui.MenuItem("Open or Create Project"u8))
                OpenOrCreateProject();

            // Does not show very first item, which is current project.
            if (_recentProjects.Count == (_project is not null ? 1 : 0))
            {
                Vector4f disabledTextColor;
                using (var style = ImGui.GetStyle())
                using (var colors = style.Colors)
                    disabledTextColor = colors[(int)ImGuiCol.ImGuiColTextDisabled];
                ImGui.PushStyleColor(ImGuiCol.ImGuiColText, disabledTextColor);
                ImGui.MenuItem("Recent Projects"u8);
                ImGui.PopStyleColor();
            }
            else if (ImGui.BeginMenu("Recent Projects"u8))
            {
                for (var index = _project is not null ? 1 : 0; index < _recentProjects.Count; ++index)
                {
                    var projectPath = _recentProjects[index];
                    var labels = GetRecentProjectLabels(projectPath);
                    if (ImGui.MenuItem(labels.Name.ContentBytes))
                        OpenProject(projectPath);

                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip(labels.Path.ContentBytes);
                }
                ImGui.Separator();
                if (ImGui.MenuItem("Clear All"u8))
                    _recentProjects.Clear();
                ImGui.EndMenu();
            }

            if (_project is not null && ImGui.MenuItem("Close Project"u8))
                _pendingCloseProject = true;

            ImGui.Separator();

            if (ImGui.MenuItem("Exit"u8))
                SendEvent<RedBolt.Input.InputEvents.ExitRequestedEvent>();

            ImGui.EndMenu();
        }

        _project?.RenderMainMenu();

        if (ImGui.BeginMenu("Tools"u8))
        {
            if (ImGui.MenuItem("Profiler"u8))
                OpenProfilerApplication();
            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu("Help"u8))
        {
            if (ImGui.MenuItem("Open Application Preferences Folder"u8))
                Reveal(Engine.GetAppPreferencesDir());
            ImGui.Separator();
            if (ImGui.MenuItem("About"u8))
                _showAbout = true;
            ImGui.EndMenu();
        }

        ImGui.EndMainMenuBar();
    }

    protected void RenderAboutDialog()
    {
        if (!_showAbout)
            return;

        ImGui.Begin("Urho3D Rebel Fork aka rbfx"u8, ref _showAbout,
            ImGuiWindowFlags.ImGuiWindowFlagsNoResize | ImGuiWindowFlags.ImGuiWindowFlagsNoCollapse);

        ImGui.Indent();
        Widgets.TextURL("GitHub page"u8, "https://github.com/rbfx/rbfx"u8);
        Widgets.TextURL("Discord server"u8, "https://discord.gg/XKs73yf"u8);
        ImGui.Unindent();

        ImGui.Separator();

        ImGui.BeginDisabled();
        ImGui.Text("Copyright © 2008-2022 the Urho3D project."u8);
        ImGui.Text("Copyright © 2017-2022 the rbfx project."u8);
        ImGui.EndDisabled();

        ImGui.End();
    }

    protected void UpdateProjectStatus()
    {
        var isHeadless = Engine.IsHeadless;

        if (_pendingCloseProject)
        {
            if (_project is not null)
            {
                var result = _project.CloseGracefully();
                if (result == CloseProjectResult.Canceled)
                {
                    _pendingCloseProject = false;
                    _pendingOpenProject = string.Empty;
                }

                if (result != CloseProjectResult.Closed)
                    return;

                CloseProject();
            }
            _pendingCloseProject = false;
        }

        // Opening a new project must be done at the point when SystemUI is not in use. End of the frame is a good
        // candidate. This subsystem will be recreated.
        if (_pendingOpenProject.Length != 0)
        {
            if (_project is not null)
            {
                _pendingCloseProject = true;
                return;
            }

            CloseProject();

            // Reset SystemUI so that imgui loads its config properly.
            if (!isHeadless)
                InitializeUI();

            var projectFlags = ProjectFlag.None;
            if (_readOnly)
                projectFlags |= ProjectFlag.ReadOnly;
            if (_singleProcess)
                projectFlags |= ProjectFlag.SingleProcess;

            _project = new EditorProject(_pendingOpenProject, _settingsJsonPath, projectFlags);
            _project.OnShallowSaved += _ => SaveTempJson();

            _recentProjects.Remove(_pendingOpenProject);
            _recentProjects.Insert(0, _pendingOpenProject);

            _pendingOpenProject = string.Empty;

            if (_command.Length != 0)
            {
                _project.ExecuteCommand(_command, _exitAfterCommand);
                _command = string.Empty;
            }
        }
    }

    protected void SaveTempJson()
    {
        using var tempFile = new JSONFile();
        tempFile.SaveObject(this);
        tempFile.SaveFile(new FileIdentifier(_tempJsonPath));
    }

    protected void OnExitRequested()
    {
        _exiting = true;
    }

    protected void OnConsoleUriClick(ref EngineEvents.ConsoleUriClickEvent args)
    {
        if (!ImGui.IsMouseClicked(ImGuiMouseButton.ImGuiMouseButtonLeft))
            return;

        var altHeld = IsKeyDown(RedBolt.Input.Key.LeftAlt) || IsKeyDown(RedBolt.Input.Key.RightAlt);
        if (!string.Equals(args.Protocol, "file", StringComparison.Ordinal))
            return;

        if (altHeld)
            Reveal(args.Address);
        else
            SystemOpen(args.Address);
    }

    protected void OpenProfilerApplication()
    {
        var programDirectory = Path.GetDirectoryName(Environment.ProcessPath) ?? string.Empty;
        var profilerPath = Path.Combine(programDirectory, OperatingSystem.IsWindows() ? "Profiler.exe" : "Profiler");
        try
        {
            Process.Start(new ProcessStartInfo(profilerPath) { UseShellExecute = false })?.Dispose();
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception
            or InvalidOperationException or NotSupportedException or FileNotFoundException)
        {
            Log.GetLogger().LogError(exception, "Failed to start profiler application at {Path}.", profilerPath);
        }
    }

    protected void OpenOrCreateProject()
    {
        // TODO: port NFD_PickFolder(), NFD_FreePath(), and the OpenProject(projectDir) call.
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static nint ReadOpen(nint context, nint handler, nint name)
    {
        return name;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void ReadLine(nint context, nint handler, nint entry, nint line)
    {
        var name = Marshal.PtrToStringUTF8(entry) ?? string.Empty;
        var lineValue = Marshal.PtrToStringUTF8(line) ?? string.Empty;
        Context.Instance.GetSubsystem<EditorProject>()?.ReadIniSettings(name, lineValue);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void WriteAll(nint context, nint handler, nint buffer)
    {
        using var output = ImGuiTextBuffer.CreateManagedWrapper(buffer, false);
        output.Append("[Project][Window]\n"u8);
        Context.Instance.GetSubsystem<EditorProject>()?.WriteIniSettings(output);
    }

    private RecentProjectLabels GetRecentProjectLabels(string projectPath)
    {
        if (_recentProjectLabels.TryGetValue(projectPath, out var labels))
            return labels;

        var trimmedPath = projectPath.TrimEnd('/', '\\');
        labels = new RecentProjectLabels(projectPath, Path.GetFileName(trimmedPath));
        _recentProjectLabels.Add(projectPath, labels);
        return labels;
    }

    private static bool IsKeyDown(RedBolt.Input.Key key)
    {
        var imguiKey = ImGui.ImGuiImplSDL2KeyEventToImGuiKey(
            (int)key, (int)RedBolt.Input.Input.GetScancodeFromKey(key));
        return ImGui.IsKeyDown(imguiKey);
    }

    private static string AddTrailingSlash(string path)
    {
        if (path.Length == 0)
            return string.Empty;
        path = path.Replace('\\', '/');
        return path.EndsWith("/", StringComparison.Ordinal) ? path : path + "/";
    }

    private static string FindResourcePrefixPath()
    {
        foreach (var path in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            for (var directory = new DirectoryInfo(Path.GetFullPath(path)); directory.Parent is not null;
                 directory = directory.Parent)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, "CoreData")))
                    return AddTrailingSlash(directory.FullName);
            }
        }
        return string.Empty;
    }

    private static void Reveal(string path)
    {
        var startInfo = new ProcessStartInfo { UseShellExecute = false };
        if (OperatingSystem.IsWindows())
        {
            startInfo.FileName = "explorer.exe";
            startInfo.ArgumentList.Add("/select,");
            startInfo.ArgumentList.Add(path);
        }
        else if (OperatingSystem.IsMacOS())
        {
            startInfo.FileName = "open";
            startInfo.ArgumentList.Add("-R");
            startInfo.ArgumentList.Add(path);
        }
        else
        {
            startInfo.FileName = "xdg-open";
            startInfo.ArgumentList.Add(Directory.Exists(path) ? path : Path.GetDirectoryName(path) ?? path);
        }
        Process.Start(startInfo)?.Dispose();
    }

    private static void SystemOpen(string path)
    {
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })?.Dispose();
    }

    /// <summary>Editor paths.</summary>
    protected string _tempJsonPath = string.Empty;
    protected string _settingsJsonPath = string.Empty;

    /// <summary>Persistent state.</summary>
    protected readonly List<string> _recentProjects = [];

    protected readonly EditorPluginManager _editorPluginManager;
    /// <summary>Currently loaded project.</summary>
    protected EditorProject? _project;

    /// <summary>Whether the editor is launched in read-only mode.</summary>
    protected bool _readOnly;
    /// <summary>Whether the editor is launched in single process mode.</summary>
    protected bool _singleProcess;
    /// <summary>Launch command and command line parameters.</summary>
    protected string _command = string.Empty;
    /// <summary>Whether to exit the editor after executing the command.</summary>
    protected bool _exitAfterCommand;

    /// <summary>UI state.</summary>
    protected string _pendingOpenProject = string.Empty;
    protected bool _pendingCloseProject;
    protected bool _exiting;

    protected bool _uiAlreadyInitialized;
    protected string _windowTitle = string.Empty;
    protected readonly Dictionary<string, Texture2D?> _projectPreviews = new(StringComparer.Ordinal);

    protected bool _showAbout;

    protected int? _numIncompleteTasks;

    private readonly Dictionary<string, RecentProjectLabels> _recentProjectLabels = new(StringComparer.Ordinal);
    private readonly ImTextureRef _projectPreviewTextureReference = new();
    private nint _settingsHandlerTypeName;
    private bool _disposed;

    private sealed class RecentProjectLabels : IDisposable
    {
        public RecentProjectLabels(string path, string name)
        {
            Path = new CString(path);
            Name = new CString(name);
        }

        public CString Path { get; }

        public CString Name { get; }

        public void Dispose()
        {
            Name.Dispose();
            Path.Dispose();
        }
    }
}
