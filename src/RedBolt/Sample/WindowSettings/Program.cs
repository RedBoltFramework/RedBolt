// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Sample.WindowSettings;

using System;
using System.Collections.Generic;

using RedBolt;
using RedBolt.Bindings.DearImGui;
using RedBolt.DearImGui;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.RenderAPI;
using RedBolt.Scene;

internal static class WindowSettingsProgram
{
    private static int Main()
    {
        try
        {
            using var application = new WindowSettingsDemo();
            var exitCode = application.Run();
            if (exitCode == 0)
                Console.WriteLine("RedBolt WindowSettings sample finished.");
            return exitCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}

/// <summary>
/// Demo application for dynamic window settings change.
/// </summary>
public class WindowSettingsDemo : Application
{
    /// <summary>
    /// Construct.
    /// </summary>
    public WindowSettingsDemo()
    {
    }

    /// <summary>
    /// Setup after engine initialization and before running the main loop.
    /// </summary>
    protected override void Start()
    {
        // Execute base class startup
        base.Start();

        // Enable OS cursor
        Input.SetMouseVisible(true);

        // Load XML file containing default UI style sheet
        // Set the loaded style as default style
        // The rbfx UI style is omitted with the allowed Dear ImGui replacement.

        // Create window with settings.
        InitSettings();
        SynchronizeSettings();
        SubscribeToEvent<GraphicsEvents.ScreenModeEvent>(
            (ref GraphicsEvents.ScreenModeEvent _) => SynchronizeSettings());

        // Set the mouse mode to use in the sample
        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(true);

        // Create scene
        CreateScene();

        // Setup viewport
        var viewport = new Viewport(_scene!, _cameraNode!.GetComponent<Camera>()!);
        Renderer.SetViewport(0, viewport);
    }

    /// <summary>
    /// Construct the scene content.
    /// </summary>
    private void CreateScene()
    {
        var cache = Resources;

        _scene = new Scene();
        _scene.CreateComponent<Octree>();

        var zone = _scene.CreateComponent<Zone>();
        zone.SetAmbientColor(Color.White);
        zone.SetBoundingBox(new BoundingBox(-1000.0f, 1000.0f));

        // Create 3D object
        var objectNode = _scene.CreateChild("Object");
        objectNode.SetRotation(new Quaternion(45.0f, 45.0f, 45.0f));
        var objectModel = objectNode.CreateComponent<StaticModel>();
        objectModel.SetModel(cache.Get<Model>("Models/Box.mdl"));
        objectModel.SetMaterial(cache.Get<Material>("Materials/Stone.xml"));

        // Create camera
        _cameraNode = _scene.CreateChild("Camera");
        _cameraNode.CreateComponent<Camera>();
        _cameraNode.SetPosition(new Vector3f(0.0f, 0.0f, -4.0f));

        // Rotate object
        SubscribeToEvent<SceneEvents.SceneUpdateEvent>(_scene,
            (ref SceneEvents.SceneUpdateEvent eventData) =>
        {
            var timeStep = eventData.TimeStep;
            objectNode.Rotate(new Quaternion(0.0f, 20.0f * timeStep, 0.0f), TransformSpace.World);
        });
    }

    /// <summary>
    /// Create window with settings.
    /// </summary>
    private void InitSettings()
    {
        Context.RegisterSubsystem(new DearImGui((ImGuiConfigFlags)Engine.Parameters.DearImGuiFlags));
        SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>(
            (ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => DrawUI());
        RefreshMonitorLabels();

        // Create the Window and add it to the UI's root node
        // Set Window size and layout settings
        // Create the Window title Text
        // Create monitor selector
        // Create resolution selector
        // Create fullscreen controller
        // Create borderless controller
        // Create resizable controller
        // Create resizable controller
        // Create multi-sample controller from 1 (= 2^0) to 16 (= 2^4)
        // Create "Apply" button
        // Apply settings when "Apply" button is clicked
        // The rbfx controls are replaced by the allowed Dear ImGui sample UI surface.
    }

    /// <summary>
    /// Synchronize settings with current state of the engine.
    /// </summary>
    private void SynchronizeSettings()
    {
        var renderDevice = GetSubsystem<RenderDevice>()!;
        var windowSettings = renderDevice.GetWindowSettings();

        // Synchronize monitor
        _monitor = windowSettings.Monitor;

        // Synchronize resolution list
        _fullscreenModes = RenderDevice.GetFullscreenModes(windowSettings.Monitor);

        // Synchronize selected resolution
        var currentSwapChainSize = renderDevice.GetSwapChainSize();
        var currentMode = new FullscreenMode(currentSwapChainSize, windowSettings.RefreshRate);
        _mode = RenderDevice.GetClosestFullscreenModeIndex(_fullscreenModes, currentMode);
        RefreshModeLabels();
        _resolutionLabel.FormatInPlace(
            $"Current: {currentMode.Size.X}x{currentMode.Size.Y}, {currentMode.RefreshRate} Hz");

        // Synchronize fullscreen and borderless flags
        _fullscreen = windowSettings.Mode == WindowMode.Fullscreen;
        _borderless = windowSettings.Mode == WindowMode.Borderless;
        _resizable = windowSettings.Resizable;
        _vSync = windowSettings.VSync;

        // Synchronize MSAA
        for (var i = 0; i <= 4; ++i)
        {
            if (windowSettings.MultiSample == 1 << i)
                _multiSample = i;
        }
    }

    private void ApplySettings()
    {
        if (_monitor < 0)
            return;

        if (_mode < 0 || _mode >= _fullscreenModes.Count)
            return;

        var windowSettings = new WindowSettings();

        if (_fullscreen)
            windowSettings.Mode = WindowMode.Fullscreen;
        else if (_borderless)
            windowSettings.Mode = WindowMode.Borderless;
        windowSettings.Resizable = _resizable;
        windowSettings.VSync = _vSync;

        windowSettings.MultiSample = _multiSample is < 0 or > 4 ? 1 : 1 << _multiSample;

        var renderDevice = GetSubsystem<RenderDevice>()!;
        var dpiScale = windowSettings.Mode == WindowMode.Fullscreen ? 1.0f : renderDevice.GetDpiScale();
        var width = MathDefs.RoundToInt(_fullscreenModes[_mode].Size.X / dpiScale);
        var height = MathDefs.RoundToInt(_fullscreenModes[_mode].Size.Y / dpiScale);
        windowSettings.Size = new Vector2i(width, height);

        windowSettings.RefreshRate = _fullscreenModes[_mode].RefreshRate;

        Graphics.SetDefaultWindowModes(windowSettings);
    }

    private void DrawUI()
    {
        var windowOpen = true;
        ImGui.SetNextWindowPos(new Vector2f(24.0f, 24.0f), ImGuiCond.ImGuiCondFirstUseEver);
        ImGui.SetNextWindowSize(new Vector2f(430.0f, 610.0f), ImGuiCond.ImGuiCondFirstUseEver);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColWindowBg, PanelBackground);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColBorder, PanelBorder);
        ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarWindowRounding, 14.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarWindowPadding, new Vector2f(20.0f, 18.0f));
        if (!ImGui.Begin("Window Settings"u8, ref windowOpen, ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            ImGui.End();
            ImGui.PopStyleVar(2);
            ImGui.PopStyleColor(2);
            return;
        }

        ImGui.PushStyleColor(ImGuiCol.ImGuiColText, AccentCyan);
        ImGui.TextUnformatted("DISPLAY CONFIGURATOR"u8);
        ImGui.PopStyleColor();
        ImGui.PushStyleColor(ImGuiCol.ImGuiColText, MutedText);
        ImGui.TextUnformatted("Preview window modes on the live 3D scene."u8);
        ImGui.PopStyleColor();
        ImGui.Separator();

        ImGui.PushStyleColor(ImGuiCol.ImGuiColText, AccentAmber);
        ImGui.TextUnformatted("CURRENT OUTPUT"u8);
        ImGui.PopStyleColor();
        ImGui.TextUnformatted(_resolutionLabel.ContentBytes);
        ImGui.Spacing();

        ImGui.TextUnformatted("DISPLAY"u8);
        ImGui.SetNextItemWidth(-1.0f);
        var monitorLabel = _monitor >= 0 && _monitor < _monitorLabels.Count
            ? _monitorLabels[_monitor].ContentBytes
            : "No displays"u8;
        if (ImGui.BeginCombo("##Monitor"u8, monitorLabel))
        {
            for (var i = 0; i < _monitorLabels.Count; ++i)
            {
                if (ImGui.Selectable(_monitorLabels[i].ContentBytes, i == _monitor))
                {
                    _monitor = i;
                    _mode = 0;
                    _fullscreenModes = RenderDevice.GetFullscreenModes(_monitor);
                    RefreshModeLabels();
                }
            }
            ImGui.EndCombo();
        }

        ImGui.TextUnformatted("RESOLUTION"u8);
        ImGui.SetNextItemWidth(-1.0f);
        var modeLabel = _mode >= 0 && _mode < _modeLabels.Count
            ? _modeLabels[_mode].ContentBytes
            : "No display modes"u8;
        if (ImGui.BeginCombo("##Resolution"u8, modeLabel))
        {
            for (var i = 0; i < _modeLabels.Count; ++i)
            {
                if (ImGui.Selectable(_modeLabels[i].ContentBytes, i == _mode))
                    _mode = i;
            }
            ImGui.EndCombo();
        }

        ImGui.Spacing();
        ImGui.PushStyleColor(ImGuiCol.ImGuiColText, AccentViolet);
        ImGui.TextUnformatted("WINDOW MODE"u8);
        ImGui.PopStyleColor();
        var windowMode = _fullscreen ? 2 : _borderless ? 1 : 0;
        ImGui.RadioButton("Windowed"u8, ref windowMode, 0);
        ImGui.SameLine();
        ImGui.RadioButton("Borderless"u8, ref windowMode, 1);
        ImGui.SameLine();
        ImGui.RadioButton("Fullscreen"u8, ref windowMode, 2);
        _fullscreen = windowMode == 2;
        _borderless = windowMode == 1;

        ImGui.Spacing();
        ImGui.PushStyleColor(ImGuiCol.ImGuiColText, AccentViolet);
        ImGui.TextUnformatted("PRESENTATION"u8);
        ImGui.PopStyleColor();
        ImGui.Checkbox("Resizable"u8, ref _resizable);
        ImGui.SameLine();
        ImGui.Checkbox("V-Sync"u8, ref _vSync);

        ImGui.TextUnformatted("ANTI-ALIASING"u8);
        ImGui.SetNextItemWidth(-1.0f);
        if (ImGui.BeginCombo("##MSAA"u8, GetMultiSampleLabel(_multiSample)))
        {
            for (var i = 0; i <= 4; ++i)
            {
                if (ImGui.Selectable(GetMultiSampleLabel(i), i == _multiSample))
                    _multiSample = i;
            }
            ImGui.EndCombo();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.PushStyleColor(ImGuiCol.ImGuiColButton, ApplyButton);
        if (ImGui.Button("APPLY DISPLAY SETTINGS"u8, new Vector2f(-1.0f, 42.0f)))
            ApplySettings();
        ImGui.PopStyleColor();
        ImGui.PushStyleColor(ImGuiCol.ImGuiColText, MutedText);
        ImGui.TextUnformatted("The scene keeps rendering while settings are applied."u8);
        ImGui.PopStyleColor();
        ImGui.End();
        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(2);
    }

    private void RefreshMonitorLabels()
    {
        DisposeLabels(_monitorLabels);
        var monitorCount = Math.Max(1, Graphics.GetMonitorCount());
        for (var i = 0; i < monitorCount; ++i)
        {
            var label = new CString();
            label.FormatInPlace($"Monitor {i + 1}");
            _monitorLabels.Add(label);
        }
    }

    private void RefreshModeLabels()
    {
        DisposeLabels(_modeLabels);
        foreach (var mode in _fullscreenModes)
        {
            var label = new CString();
            label.FormatInPlace($"{mode.Size.X} x {mode.Size.Y} @ {mode.RefreshRate} Hz");
            _modeLabels.Add(label);
        }
    }

    private static void DisposeLabels(List<CString> labels)
    {
        foreach (var label in labels)
            label.Dispose();
        labels.Clear();
    }

    private static ReadOnlySpan<byte> GetMultiSampleLabel(int index) => index switch
    {
        1 => "2x MSAA"u8,
        2 => "4x MSAA"u8,
        3 => "8x MSAA"u8,
        4 => "16x MSAA"u8,
        _ => "Off"u8
    };

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape))
            Exit();
    }

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.WindowSettings";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.WindowMaximize = false;
        parameters.ResourcePaths = "CoreData;SampleData";
        parameters.ResourcePrefixPaths = ".;..";
#if !REDBOLT_PLATFORM_BROWSER
            parameters.WindowHeight = 720;
            parameters.WindowWidth = 1280;
#endif
        parameters.WindowTitle = "RedBolt Window Settings";
    }

    protected override void Stop()
    {
        _resolutionLabel.Dispose();
        DisposeLabels(_monitorLabels);
        DisposeLabels(_modeLabels);
        Renderer.SetNumViewports(0);
        _cameraNode = null;
        _scene?.Dispose();
        _scene = null;
        base.Stop();
    }

    public override void Dispose()
    {
        _resolutionLabel.Dispose();
        DisposeLabels(_monitorLabels);
        DisposeLabels(_modeLabels);
        if (_scene is not null)
            Renderer.SetNumViewports(0);
        _cameraNode = null;
        _scene?.Dispose();
        _scene = null;
        base.Dispose();
    }

    private Scene? _scene;
    private Node? _cameraNode;
    private int _monitor;
    private readonly CString _resolutionLabel = new();
    private int _mode;
    private List<FullscreenMode> _fullscreenModes = [];
    private readonly List<CString> _monitorLabels = [];
    private readonly List<CString> _modeLabels = [];
    private bool _fullscreen;
    private bool _borderless;
    private bool _resizable;
    private bool _vSync;
    private int _multiSample;
    private static readonly Vector4f PanelBackground = new(0.028f, 0.038f, 0.07f, 0.96f);
    private static readonly Vector4f PanelBorder = new(0.19f, 0.25f, 0.43f, 0.9f);
    private static readonly Vector4f MutedText = new(0.56f, 0.62f, 0.74f, 1.0f);
    private static readonly Vector4f AccentCyan = new(0.18f, 0.92f, 1.0f, 1.0f);
    private static readonly Vector4f AccentViolet = new(0.68f, 0.47f, 1.0f, 1.0f);
    private static readonly Vector4f AccentAmber = new(1.0f, 0.68f, 0.26f, 1.0f);
    private static readonly Vector4f ApplyButton = new(0.12f, 0.45f, 0.52f, 1.0f);
}
