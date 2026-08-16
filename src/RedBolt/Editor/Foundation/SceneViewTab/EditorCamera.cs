// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Editor.Foundation;

using System;
using System.Runtime.CompilerServices;

using RedBolt.Editor.Foundation.Shared;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Math;

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
