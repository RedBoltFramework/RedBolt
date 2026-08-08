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

namespace RedBolt.Editor.Foundation;

using System;

using Microsoft.Extensions.Logging;

using RedBolt.Bindings.DearImGui;
using RedBolt.Core;
using RedBolt.Editor.Core;
using RedBolt.Editor.Project;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.RenderAPI;
using RedBolt.SystemUI;
using RedBolt.Utility;
using DearImGuiSubsystem = RedBolt.DearImGui.DearImGui;
using EngineSubsystem = RedBolt.Engine.Engine;
using ProjectSubsystem = RedBolt.Editor.Project.Project;
using RmlUISubsystem = RedBolt.RmlUI.RmlUI;

/// <summary>Tab that renders Scene and enables Scene manipulation.</summary>
public class GameViewTab : EditorTab
{
    private static readonly EditorHotkey HotkeyReleaseInput =
        new EditorHotkey("GameViewTab.ReleaseInput").Shift().Press(Key.Escape);

    public event Action? OnSimulationStarted;
    public event Action? OnSimulationStopped;

    public GameViewTab()
        : base("Game", "212a6577-8a2a-42d6-aaed-042d226c724c",
            EditorTabFlag.NoContentPadding | EditorTabFlag.OpenByDefault, EditorTabPlacement.DockCenter)
    {
        _backbuffer = new CustomBackbufferTexture();
        _textureReference = new ImTextureRef();

        BindHotkey(HotkeyReleaseInput, ReleaseInput);

        // TODO: port reloadable user plugins.
        // auto pluginManager = GetSubsystem<PluginManager>();
        // pluginManager->SetQuitApplicationCallback([this] { QuitApplication(); });
    }

    public override void Dispose()
    {
        // TODO: port reloadable user plugins.
        // auto pluginManager = GetSubsystem<PluginManager>();
        // pluginManager->SetQuitApplicationCallback([] {});

        _state?.Dispose();
        _state = null;
        _textureReference.Dispose();
        _backbuffer.Dispose();
        OnSimulationStopped = null;
        OnSimulationStarted = null;
        base.Dispose();
    }

    public void Play()
    {
        if (_state is not null)
            Stop();

        _state = new PlayState(_backbuffer);
        OnSimulationStarted?.Invoke();
    }

    public bool IsPlaying() => _state is not null;

    public bool IsInputGrabbed()
    {
        return _state is not null && _state.IsInputGrabbed();
    }

    /// <summary>Commands.</summary>
    /// <remarks>Stop simulation.</remarks>
    public void Stop()
    {
        if (_state is null)
            return;

        _state.Dispose();
        _state = null;
        OnSimulationStopped?.Invoke();
    }

    public void TogglePlayed()
    {
        if (IsPlaying())
            Stop();
        else
            Play();
    }

    public void ReleaseInput()
    {
        _state?.ReleaseInput();
    }

    /// <summary>Implement EditorTab.</summary>
    public override void RenderToolbar()
    {
        if (Widgets.ToolbarButton("\uf188"u8, "Toggle Debug HUD"u8, _hudVisible))
            _hudVisible = !_hudVisible;

        Widgets.ToolbarSeparator();
    }

    public override void RenderContent()
    {
        var contentSize = GetContentSize();
        if (contentSize.X == 0 || contentSize.Y == 0)
            return;

        _backbuffer.SetTextureSize(contentSize);
        _backbuffer.Update();

        if (_state is not null)
        {
            var io = ImGui.GetIO();
            var sceneTexture = _backbuffer.Texture;
            Widgets.ImageItem(
                sceneTexture, _textureReference, new Vector2f(sceneTexture.Width, sceneTexture.Height));

            var origin = Vector2i.Zero;
            if ((io.ConfigFlags & ImGuiConfigFlags.ImGuiConfigFlagsViewportsEnable) == 0)
            {
                var graphics = GetSubsystem<Graphics>();
                origin = graphics.GetWindowPosition();
            }

            var windowMin = origin + Vector2f.RoundToInt(ImGui.GetItemRectMin());
            var windowMax = origin + Vector2f.RoundToInt(ImGui.GetItemRectMax());
            _state.Update(new IntRect(windowMin, windowMax));
        }

        if (_state is not null)
        {
            var wasGrabbed = _state.IsInputGrabbed();
            var needGrab = ImGui.IsItemHovered() && IsAnyMouseButtonClicked();
            var needRelease = !ImGui.IsItemHovered() && IsAnyMouseButtonClicked();
            if (!wasGrabbed && needGrab)
                _state.GrabInput();
            else if (wasGrabbed && needRelease)
                _state.ReleaseInput();
        }

        var hud = GetSubsystem<DebugHud>();
        if (hud is not null && _hudVisible)
        {
            var position = GetContentPosition();
            ImGui.SetCursorScreenPos(position.ToVector2());
            hud.RenderUI(DebugHudMode.All);
        }
    }

    public override void RenderContextMenuItems()
    {
    }

    public override void WriteIniSettings(ImGuiTextBuffer output)
    {
        IniHelpers.WriteIntToIni(output, "IsHudVisible", _hudVisible ? 1 : 0);
    }

    public override void ReadIniSettings(string line)
    {
        var isHudVisible = IniHelpers.ReadIntFromIni(line, "IsHudVisible");
        if (isHudVisible.HasValue)
            _hudVisible = isHudVisible.Value != 0;
    }

    private void QuitApplication()
    {
        var workQueue = GetSubsystem<WorkQueue>();
        if (IsPlaying())
            workQueue.PostDelayedTaskForMainThread(() => Stop());
        else
            Log.GetLogger().LogWarning("Application quit was requested when nothing is played");
    }

    private static bool IsAnyMouseButtonClicked()
    {
        return ImGui.IsMouseClicked(ImGuiMouseButton.ImGuiMouseButtonLeft)
            || ImGui.IsMouseClicked(ImGuiMouseButton.ImGuiMouseButtonRight)
            || ImGui.IsMouseClicked(ImGuiMouseButton.ImGuiMouseButtonMiddle)
            || ImGui.IsMouseClicked((ImGuiMouseButton)3)
            || ImGui.IsMouseClicked((ImGuiMouseButton)4);
    }

    private sealed class PlayState : RedObject
    {
        public PlayState(CustomBackbufferTexture backbuffer)
        {
            _engine = GetSubsystem<EngineSubsystem>();
            _renderer = GetSubsystem<Renderer>();
            // TODO: port reloadable user plugins.
            // pluginManager_(GetSubsystem<PluginManager>())
            _input = GetSubsystem<Input>();
            // TODO: built-in UI is excluded from the managed port.
            // legacyUI_(GetSubsystem<UI>())
            _systemUI = GetSubsystem<DearImGuiSubsystem>();
            _rmlUI = GetSubsystem<RmlUISubsystem>();
            _stateManager = GetSubsystem<StateManager>();
            _project = GetSubsystem<ProjectSubsystem>();
            _backbuffer = backbuffer;
            _renderTargets = new RenderTargetView[1];

            _engine.SetParameter(EngineParameter.IsRunningInEditor, true);

            UpdateRenderSurface();

            // TODO: built-in UI is excluded from the managed port.
            // legacyUI_->GetRoot()->RemoveAllChildren();
            // legacyUI_->GetRootModalElement()->RemoveAllChildren();
            // legacyUI_->SetRenderTarget(backbuffer_->GetTexture());
            _backbuffer.SetActive(true);
            GrabInput();

            // TODO: port reloadable user plugins.
            // pluginManager_->StartApplication();
            UpdatePreferredMouseSetup();

            SubscribeToEvent<GraphicsEvents.BeginRenderingEvent>((ref GraphicsEvents.BeginRenderingEvent _) =>
            {
                var renderDevice = GetSubsystem<RenderDevice>();
                var renderContext = renderDevice.GetRenderContext();

                _renderTargets[0] = RenderTargetView.Texture(_backbuffer.Texture);
                renderContext.SetRenderTargets(default, _renderTargets);
                renderContext.ClearRenderTarget(0, new Color(0x245953u, Color.RGB));

                UnsubscribeFromEvent<GraphicsEvents.BeginRenderingEvent>();
            });
        }

        public void GrabInput()
        {
            if (_inputGrabbed)
                return;

            _input.SetMouseVisible(_preferredMouseVisible);
            _input.SetMouseMode(_preferredMouseMode);
            _input.SetEnabled(true);
            _systemUI.PassThroughEvents = true;
            _project.SetGlobalHotkeysEnabled(false);
            _project.SetHighlightEnabled(true);

            _inputGrabbed = true;
        }

        public void ReleaseInput()
        {
            if (!_inputGrabbed)
                return;

            UpdatePreferredMouseSetup();
            _input.SetMouseVisible(true);
            _input.SetMouseMode(MouseMode.Absolute);
            _input.SetEnabled(false);
            _systemUI.PassThroughEvents = false;
            _project.SetGlobalHotkeysEnabled(true);
            _project.SetHighlightEnabled(false);

            _inputGrabbed = false;
        }

        public void Update(IntRect windowRect)
        {
            _input.SetExplicitWindowRect(windowRect);
            UpdateRenderSurface();
        }

        public bool IsInputGrabbed() => _inputGrabbed;

        public override void Dispose()
        {
            ReleaseInput();

            // TODO: port reloadable user plugins.
            // pluginManager_->StopApplication();
            _backbuffer.SetActive(false);

            // TODO: built-in UI is excluded from the managed port.
            // legacyUI_->SetRenderTarget(nullptr);
            // legacyUI_->SetCustomSize({0, 0});
            // legacyUI_->GetRoot()->RemoveAllChildren();
            // legacyUI_->GetRootModalElement()->RemoveAllChildren();

            _rmlUI.SetRenderTarget();

            _input.ResetExplicitWindowRect();

            _renderer.SetBackbufferRenderSurface(null);
            _renderer.SetNumViewports(0);

            _stateManager.Reset();

            _engine.SetParameter(EngineParameter.IsRunningInEditor, null);

            _backbufferSurface?.SetTarget(null!);
            _backbufferSurface = null;
            base.Dispose();
        }

        private void UpdatePreferredMouseSetup()
        {
            _preferredMouseVisible = _input.IsMouseVisible();
            _preferredMouseMode = _input.GetMouseMode();
        }

        private void UpdateRenderSurface()
        {
            var backbufferSurface = _backbuffer.Texture.GetRenderSurface();
            RenderSurface? previousBackbufferSurface = null;
            _backbufferSurface?.TryGetTarget(out previousBackbufferSurface);
            if (!ReferenceEquals(previousBackbufferSurface, backbufferSurface))
            {
                _backbufferSurface = backbufferSurface is null ? null : new WeakReference<RenderSurface>(backbufferSurface);
                _renderer.SetBackbufferRenderSurface(backbufferSurface);
                // TODO: built-in UI is excluded from the managed port.
                // legacyUI_->SetCustomSize(backbufferSurface->GetSize());
                _rmlUI.SetRenderTarget(backbufferSurface);
            }
        }

        private readonly EngineSubsystem _engine;
        private readonly Renderer _renderer;
        // TODO: port PluginManager* pluginManager_ with reloadable user plugins.
        private readonly Input _input;
        // TODO: built-in UI is excluded from the managed port.
        // UI* legacyUI_{};
        private readonly DearImGuiSubsystem _systemUI;
        private readonly RmlUISubsystem _rmlUI;
        private readonly StateManager _stateManager;
        private readonly ProjectSubsystem _project;

        private readonly CustomBackbufferTexture _backbuffer;
        private WeakReference<RenderSurface>? _backbufferSurface;

        private bool _inputGrabbed;

        private bool _preferredMouseVisible = true;
        private MouseMode _preferredMouseMode = MouseMode.Free;

#pragma warning disable CS0169
        private Vector2i _oldRootElementSize;
#pragma warning restore CS0169
        private readonly RenderTargetView[] _renderTargets;
    }

    private readonly CustomBackbufferTexture _backbuffer;
    private readonly ImTextureRef _textureReference;

    private PlayState? _state;
    private bool _hudVisible;
}
