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
using System.Runtime.CompilerServices;

using RedBolt.Editor.Foundation.Shared;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Math;

// TODO: port Foundation_EditorCamera when editor plugins are ported.
// void Foundation_EditorCamera(Context* context, SceneViewTab* sceneViewTab)
// {
//     auto project = sceneViewTab->GetProject();
//     auto settingsManager = project->GetSettingsManager();
//
//     auto settingsPage = MakeShared<EditorCamera::SettingsPage>(context);
//     settingsManager->AddPage(settingsPage);
//
//     sceneViewTab->RegisterAddon<EditorCamera>(settingsPage);
// }

/// <summary>Camera controller used by Scene View.</summary>
public class EditorCamera : SceneViewAddon
{
    public EditorCamera(SceneViewTab owner, CameraController.SettingsPage settings)
        : base(owner)
    {
        _settings = new WeakReference<CameraController.SettingsPage>(settings);
        _cameraController = new CameraController(owner.GetHotkeyManager());
        owner.OnLookAt += LookAtPosition;
    }

    public override void Dispose()
    {
        if (_owner.TryGetTarget(out var owner))
            owner.OnLookAt -= LookAtPosition;
        _cameraController.Dispose();
        _settings.SetTarget(null!);
        base.Dispose();
    }

    /// <summary>Implement SceneViewAddon.</summary>
    public override string GetUniqueName() => "Camera";

    public override int GetInputPriority() => int.MaxValue;

    public override void ProcessInput(SceneViewPage scenePage, ref bool mouseConsumed)
    {
        ref var state = ref GetOrInitializeState(scenePage);
        var camera = scenePage.Renderer.Camera!;
        if (_settings.TryGetTarget(out var settings))
            _cameraController.ProcessInput(camera, ref state, settings.Values);
    }

    public override void SerializePageState(
        Archive archive, string name, ref object? stateWrapped)
    {
        stateWrapped ??= new CameraController.PageState();
        ref var state = ref Unsafe.Unbox<CameraController.PageState>(stateWrapped);
        using var block = archive.OpenUnorderedBlock(name);
        state.SerializeInBlock(archive);
    }

    private ref CameraController.PageState GetOrInitializeState(SceneViewPage scenePage)
    {
        ref var stateWrapped = ref scenePage.GetAddonData(this);
        stateWrapped ??= new CameraController.PageState();
        return ref Unsafe.Unbox<CameraController.PageState>(stateWrapped);
    }

    private void LookAtPosition(SceneViewPage scenePage, Vector3f position)
    {
        ref var state = ref GetOrInitializeState(scenePage);
        var camera = scenePage.Renderer.Camera!;
        if (!_settings.TryGetTarget(out var settings))
            return;

        ref readonly var config = ref settings.Values;
        var node = camera.Node;

        var newPosition = position
            - node.Rotation * new Vector3f(0.0f, 0.0f, config.FocusDistance);
        state.PendingOffset += newPosition - state.LastCameraPosition;
    }

    private readonly WeakReference<CameraController.SettingsPage> _settings;
    private readonly CameraController _cameraController;
#pragma warning disable CS0169 // Preserved native isActive_ member is currently unused in rbfx as well.
    private bool _isActive;
#pragma warning restore CS0169
}
