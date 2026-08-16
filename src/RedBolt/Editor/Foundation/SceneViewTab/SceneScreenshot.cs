// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Editor.Foundation;

using System;
using System.Collections.Generic;

using Microsoft.Extensions.Logging;

using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Editor.Core;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.IO;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.RenderAPI;
using RedBolt.Resource;
using RedBolt.Scene;
using RedBolt.SystemUI;
using RedBolt.Utility;

/// <summary>Add-on that takes high-resolution screenshots from the scene.</summary>
public class SceneScreenshot : SceneViewAddon
{
    private static readonly EditorHotkey HotkeyTakeScreenshot =
        new EditorHotkey("SceneScreenshot.Take").Shift().Press(Key.P);

    private static readonly byte[][] ResolutionLabels =
    [
        "Custom"u8.ToArray(),
        "FullHD"u8.ToArray(),
        "FullHD Portrait"u8.ToArray(),
        "4K"u8.ToArray(),
        "4K Portrait"u8.ToArray(),
        "2K Square"u8.ToArray(),
        "Android Feature"u8.ToArray(),
        "Android TV"u8.ToArray(),
        "Youtube banner"u8.ToArray(),
        "Youtube watermark"u8.ToArray(),
    ];

    private static readonly Vector2i[] Resolutions =
    [
        Vector2i.Zero,
        new(1920, 1080),
        new(1080, 1920),
        new(3840, 2160),
        new(2160, 3840),
        new(2048, 2048),
        new(1024, 500),
        new(1280, 720),
        new(2048, 1152),
        new(150, 150),
    ];

    private static ReadOnlySpan<byte> PopupName => "Take Screenshot from Scene"u8;

    private sealed class ScreenshotRenderer : RedObject
    {
        public event Action<ScreenshotRenderer, Image>? OnReady;

        public ScreenshotRenderer(Scene scene, Camera camera, Vector2i resolution)
        {
            _oldDebugGeometry = camera.GetDrawDebugGeometry();
            _oldAutoAspectRatio = camera.GetAutoAspectRatio();
            _oldAspectRatio = camera.GetAspectRatio();
            _camera = new WeakReference<Camera>(camera);
            _texture = new Texture2D();

            camera.SetDrawDebugGeometry(false);
            camera.SetAspectRatio(resolution.X / (float)resolution.Y);

            _texture.SetSize(
                resolution.X,
                resolution.Y,
                TextureFormat.TexFormatRgba8Unorm,
                TextureFlag.BindRenderTarget);
            SubscribeToEvent<GraphicsEvents.EndViewRenderEvent>(
                _texture, (ref GraphicsEvents.EndViewRenderEvent _) => OnEndViewRender());

            _viewport = new Viewport(scene, camera);

            var surface = _texture.GetRenderSurface();
            surface?.SetViewport(0, _viewport);

            var renderer = GetSubsystem<Renderer>();
            renderer?.QueueRenderSurface(surface);
        }

        public override void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            UnsubscribeFromEvent<GraphicsEvents.EndViewRenderEvent>();
            OnReady = null;

            if (_camera.TryGetTarget(out var camera) && camera is not null)
            {
                camera.SetDrawDebugGeometry(_oldDebugGeometry);
                camera.SetAspectRatio(_oldAspectRatio);
                camera.SetAutoAspectRatio(_oldAutoAspectRatio);
            }

            _texture.GetRenderSurface()?.SetNumViewports(0);
            _viewport.Dispose();
            _texture.Dispose();
            _camera.SetTarget(null!);
            base.Dispose();
        }

        private void OnEndViewRender()
        {
            if (_completed || _disposed)
                return;
            _completed = true;

            using var image = _texture.GetImage();
            if (image is not null)
                OnReady?.Invoke(this, image);
            OnReady = null;

            // Postpone deletion of this object until the end of frame.
            GetSubsystem<WorkQueue>()?.PostDelayedTaskForMainThread(Dispose);
        }

        private readonly bool _oldDebugGeometry;
        private readonly bool _oldAutoAspectRatio;
        private readonly float _oldAspectRatio;
        private readonly WeakReference<Camera> _camera;

        private readonly Texture2D _texture;
        private readonly Viewport _viewport;

        private bool _completed;
        private bool _disposed;
    }

    public SceneScreenshot(SceneViewTab owner)
        : base(owner)
    {
        var hotkeyManager = owner.GetHotkeyManager();
        hotkeyManager.BindHotkey(
            this, HotkeyTakeScreenshot, static self => self.TakeScreenshotWithPopup());
    }

    public override void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        for (var index = _screenshotRenderers.Count - 1; index >= 0; --index)
            _screenshotRenderers[index].Dispose();
        _screenshotRenderers.Clear();

        _editorCamera.SetTarget(null!);
        _sceneCamera.SetTarget(null!);
        _scene.SetTarget(null!);
        _hotkeyLabel.Dispose();
        _textBuffer.Dispose();
        base.Dispose();
    }

    public void TakeScreenshotWithPopup()
    {
        _openPending = true;
    }

    public void TakeScreenshotDelayed(Scene scene, Camera? camera, Vector2i resolution)
    {
        var sceneReference = new WeakReference<Scene>(scene);
        var cameraReference = new WeakReference<Camera>(camera!);
        var queue = GetSubsystem<WorkQueue>();
        queue?.PostDelayedTaskForMainThread(() =>
        {
            if (!_disposed
                && sceneReference.TryGetTarget(out var targetScene)
                && targetScene is not null
                && cameraReference.TryGetTarget(out var targetCamera)
                && targetCamera is not null)
            {
                TakeScreenshotNow(targetScene, targetCamera, resolution);
            }
        });
    }

    public void TakeScreenshotNow(Scene scene, Camera camera, Vector2i resolution)
    {
        var screenshotRenderer = new ScreenshotRenderer(scene, camera, resolution);
        _screenshotRenderers.Add(screenshotRenderer);
        screenshotRenderer.OnReady += OnScreenshotReady;
    }

    /// <summary>Implement SceneViewAddon.</summary>
    public override string GetUniqueName() => "Screenshot";

    public override void Render(SceneViewPage page)
    {
        if (_openPending)
        {
            ImGui.OpenPopup(PopupName);
            InitializePopup(page);
        }
        _openPending = false;

        if (ImGui.BeginPopup(
                PopupName, flags: ImGuiWindowFlags.ImGuiWindowFlagsAlwaysAutoResize))
        {
            RenderPopup();
            ImGui.EndPopup();
        }
    }

    public override void ApplyHotkeys(HotkeyManager hotkeyManager)
    {
        hotkeyManager.InvokeFor(this);
    }

    public override bool RenderTabContextMenu()
    {
        if (_owner.TryGetTarget(out var owner))
        {
            var hotkeyManager = owner.GetHotkeyManager();
            _hotkeyLabel.Set(hotkeyManager.GetHotkeyLabel(HotkeyTakeScreenshot));
            if (ImGui.MenuItem("Take Screenshot..."u8, _hotkeyLabel.ContentBytes))
                TakeScreenshotWithPopup();
        }
        return true;
    }

    private void OnScreenshotReady(ScreenshotRenderer screenshotRenderer, Image image)
    {
        var fileName = GenerateFileName(new Vector2i(image.Width, image.Height));
        if (fileName.Length != 0)
        {
            image.SaveFile(FileIdentifier.FromUri(fileName));
            Log.GetLogger().LogInformation("Screenshot saved to file://{FileName}", fileName);
        }

        // Remove this callback and ScreenshotRenderer with it.
        screenshotRenderer.OnReady -= OnScreenshotReady;
        _screenshotRenderers.Remove(screenshotRenderer);
    }

    private string GenerateFileName(Vector2i size)
    {
        if (!_owner.TryGetTarget(out var owner))
            return string.Empty;

        var project = owner.GetProject();
        var outputPath = project.ArtifactsPath;
        var screenshotTime = Time.GetTimeStamp("yyyy-MM-dd/yyyy-MM-dd_HH-mm-ss");
        var fileNamePrefix =
            $"{outputPath}Screenshots/{screenshotTime}_{size.X}x{size.Y}";
        const string fileNameSuffix = ".png";

        const uint maxAttempts = 100;
        for (uint index = 0; index < maxAttempts; ++index)
        {
            var fileName = index != 0
                ? $"{fileNamePrefix}_{index}{fileNameSuffix}"
                : $"{fileNamePrefix}{fileNameSuffix}";
            if (global::System.IO.File.Exists(fileName))
                continue;

            return fileName;
        }

        Log.GetLogger().LogError(
            "Screenshot file already exists: {FileNamePrefix}{FileNameSuffix}",
            fileNamePrefix,
            fileNameSuffix);
        return string.Empty;
    }

    private Camera? FindCameraInSelection(SceneSelection selection)
    {
        // Try component first.
        foreach (var componentReference in selection.Components)
        {
            if (componentReference.TryGetTarget(out var component) && component is Camera camera)
                return camera;
        }

        // Then try to find camera in selected nodes directly, ignoring the Scene.
        foreach (var nodeReference in selection.EffectiveNodes)
        {
            if (nodeReference.TryGetTarget(out var node) && node.GetComponent<Camera>() is { } camera)
                return camera;
        }

        // Then try to find camera in selected nodes' children, ignoring the Scene.
        foreach (var nodeReference in selection.EffectiveNodes)
        {
            if (nodeReference.TryGetTarget(out var node) && node.FindComponent<Camera>() is { } camera)
                return camera;
        }

        return null;
    }

    private void InitializePopup(SceneViewPage page)
    {
        _scene.SetTarget(page.Scene);
        _sceneCamera.SetTarget(FindCameraInSelection(page.Selection)!);
        _editorCamera.SetTarget(page.Renderer.Camera!);
        if (!_sceneCamera.TryGetTarget(out var sceneCamera) || sceneCamera is null)
            _useInSceneCamera = false;
    }

    private void RenderPopup()
    {
        if (!_scene.TryGetTarget(out var scene)
            || scene is null
            || !_editorCamera.TryGetTarget(out var editorCamera)
            || editorCamera is null)
        {
            ImGui.CloseCurrentPopup();
            return;
        }

        var hasInSceneCamera =
            _sceneCamera.TryGetTarget(out var sceneCamera) && sceneCamera is not null;
        ImGui.BeginDisabled(!hasInSceneCamera);
        ImGui.Checkbox("Use in-Scene Camera"u8, ref _useInSceneCamera);
        if (hasInSceneCamera)
        {
            using var guard = new ColorScopeGuard(
                ImGuiCol.ImGuiColText, new Vector4f(1.00f, 1.00f, 0.35f, 1.00f));
            _textBuffer.Set(sceneCamera!.GetFullNameDebug());
            ImGui.TextUnformatted(_textBuffer.ContentBytes);
        }
        ImGui.EndDisabled();

        ImGui.Separator();
        if (ImGui.BeginCombo("Resolution"u8, ResolutionLabels[_resolutionOption]))
        {
            if (ImGui.Selectable(ResolutionLabels[0], _resolutionOption == 0))
                _resolutionOption = 0;
            for (var index = 1; index < Resolutions.Length; ++index)
            {
                if (ImGui.Selectable(
                        ResolutionLabels[index], _resolutionOption == (uint)index))
                {
                    _resolutionOption = (uint)index;
                    _resolution = Resolutions[index];
                }
            }
            ImGui.EndCombo();
        }

        if (_resolutionOption == 0)
        {
            ImGui.TextUnformatted("Resolution: "u8);
            ImGui.SameLine();
            ImGui.PushItemWidth(100);
            ImGui.InputInt("##width"u8, ref _resolution.X, 0);
            ImGui.SameLine();
            ImGui.TextUnformatted("x"u8);
            ImGui.SameLine();
            ImGui.InputInt("##height"u8, ref _resolution.Y, 0);
            ImGui.PopItemWidth();
        }
        else
        {
            _textBuffer.FormatInPlace($"Resolution: {_resolution.X}x{_resolution.Y}");
            ImGui.TextUnformatted(_textBuffer.ContentBytes);
        }

        _resolution = Vector2i.Max(
            Vector2i.One, Vector2i.Min(_resolution, new Vector2i(8192, 8192)));

        ImGui.Separator();

        ImGui.Checkbox("Keep This Window Open"u8, ref _keepPopupOpen);

        ImGui.Separator();

        if (ImGui.Button("\uf030 Take"u8)
            || IsKeyPressed(Key.Return)
            || IsKeyPressed(Key.Return2))
        {
            TakeScreenshotDelayed(
                scene, _useInSceneCamera ? sceneCamera : editorCamera, _resolution);
            if (!_keepPopupOpen)
                ImGui.CloseCurrentPopup();
        }
        ImGui.SameLine();
        if (ImGui.Button("\uf05e Cancel"u8) || IsKeyPressed(Key.Escape))
            ImGui.CloseCurrentPopup();
    }

    private static bool IsKeyPressed(Key key)
    {
        var imguiKey = ImGui.ImGuiImplSDL2KeyEventToImGuiKey(
            (int)key, (int)Input.GetScancodeFromKey(key));
        return (int)imguiKey != 0 && ImGui.IsKeyPressed(imguiKey);
    }

    private bool _openPending;

    private bool _keepPopupOpen;
    private bool _useInSceneCamera = true;
    private uint _resolutionOption;
    private Vector2i _resolution = new(1920, 1080);

    private readonly WeakReference<Scene> _scene = new(null!);
    private readonly WeakReference<Camera> _sceneCamera = new(null!);
    private readonly WeakReference<Camera> _editorCamera = new(null!);

    private readonly List<ScreenshotRenderer> _screenshotRenderers = [];
    private readonly CString _textBuffer = new();
    private readonly CString _hotkeyLabel = new();
    private bool _disposed;
}
