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

namespace RedBolt.Editor.Foundation.Shared;

using System;

using RedBolt.Bindings.DearImGui;
using RedBolt.Core;
using RedBolt.Editor.Core;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.Scene;

using DearImGuiSubsystem = global::RedBolt.DearImGui.DearImGui;
using EditorProject = global::RedBolt.Editor.Project.Project;

/// <summary>Camera controller used by Scene View.</summary>
public class CameraController : RedObject
{
    public struct Settings : ISimpleSettingsPageValues
    {
        public Settings()
        {
            MouseSensitivity = 0.25f;
            MinSpeed = 2.0f;
            MaxSpeed = 10.0f;
            ScrollSpeed = 3.5f;
            Acceleration = 1.0f;
            ShiftFactor = 4.0f;
            FocusDistance = 10.0f;
            FocusSpeed = 17.0f;
            Orthographic = false;
            OrthoSize = 10.0f;
        }

        public string UniqueName => "Editor.Scene:Camera";

        public void SerializeInBlock(Archive archive)
        {
            ArchiveSerialization.SerializeOptionalValue(
                archive, "MouseSensitivity", ref MouseSensitivity, new Settings().MouseSensitivity,
                ArchiveSerialization.SerializeValue);
            ArchiveSerialization.SerializeOptionalValue(
                archive, "MinSpeed", ref MinSpeed, new Settings().MinSpeed,
                ArchiveSerialization.SerializeValue);
            ArchiveSerialization.SerializeOptionalValue(
                archive, "MaxSpeed", ref MaxSpeed, new Settings().MaxSpeed,
                ArchiveSerialization.SerializeValue);
            ArchiveSerialization.SerializeOptionalValue(
                archive, "ScrollSpeed", ref ScrollSpeed, new Settings().ScrollSpeed,
                ArchiveSerialization.SerializeValue);
            ArchiveSerialization.SerializeOptionalValue(
                archive, "Acceleration", ref Acceleration, new Settings().Acceleration,
                ArchiveSerialization.SerializeValue);
            ArchiveSerialization.SerializeOptionalValue(
                archive, "ShiftFactor", ref ShiftFactor, new Settings().ShiftFactor,
                ArchiveSerialization.SerializeValue);
            ArchiveSerialization.SerializeOptionalValue(
                archive, "FocusDistance", ref FocusDistance, new Settings().FocusDistance,
                ArchiveSerialization.SerializeValue);
            ArchiveSerialization.SerializeOptionalValue(
                archive, "Orthographic", ref Orthographic, new Settings().Orthographic,
                ArchiveSerialization.SerializeValue);
            ArchiveSerialization.SerializeOptionalValue(
                archive, "OrthographicSize", ref OrthoSize, new Settings().OrthoSize,
                ArchiveSerialization.SerializeValue);
        }

        public void RenderSettings()
        {
            ImGui.DragFloat("Mouse Sensitivity"u8, ref MouseSensitivity, 0.01f, 0.0f, 1.0f, "%.2f"u8);
            ImGui.DragFloat("Min Speed"u8, ref MinSpeed, 0.1f, 0.1f, 100.0f, "%.1f"u8);
            ImGui.DragFloat("Max Speed"u8, ref MaxSpeed, 0.1f, 0.1f, 100.0f, "%.1f"u8);
            ImGui.DragFloat("Scroll Speed"u8, ref ScrollSpeed, 0.1f, 0.1f, 100.0f, "%.1f"u8);
            ImGui.DragFloat("Acceleration"u8, ref Acceleration, 0.1f, 0.1f, 100.0f, "%.1f"u8);
            ImGui.DragFloat("Shift Factor"u8, ref ShiftFactor, 0.5f, 1.0f, 10.0f, "%.1f"u8);
            ImGui.DragFloat("Focus Distance"u8, ref FocusDistance, 0.1f, 0.1f, 100.0f, "%.1f"u8);
            ImGui.Checkbox("Orthographic"u8, ref Orthographic);
            ImGui.InputFloat("Orthographic Size"u8, ref OrthoSize, 0.1f, 1.0f, "%.1f"u8);
        }

        public float MouseSensitivity;
        public float MinSpeed;
        public float MaxSpeed;
        public float ScrollSpeed;
        public float Acceleration;
        public float ShiftFactor;
        public float FocusDistance;
        public float FocusSpeed;
        public bool Orthographic;
        public float OrthoSize;
    }

    public class SettingsPage : SimpleSettingsPage<Settings>
    {
    }

    public struct PageState
    {
        public Vector3f LastCameraPosition;
        public Quaternion LastCameraRotation;
        public float Yaw;
        public float Pitch;
        public float CurrentMoveSpeed;
        public Vector3f PendingOffset;
        public Vector3f? OrbitPosition;

        public PageState()
        {
            LastCameraPosition = new Vector3f();
            LastCameraRotation = new Quaternion();
            Yaw = 0.0f;
            Pitch = 0.0f;
            CurrentMoveSpeed = 0.0f;
            PendingOffset = new Vector3f();
            OrbitPosition = null;

            LookAt(new Vector3f(0.0f, 5.0f, -10.0f), Vector3f.Zero);
        }

        public void LookAt(Vector3f position, Vector3f target)
        {
            LastCameraPosition = position;
            LastCameraRotation.FromLookRotation(target - position, Vector3f.Up);
            Yaw = LastCameraRotation.YawAngle;
            Pitch = LastCameraRotation.PitchAngle;
        }

        public void LookAt(BoundingBox box)
        {
            var center = box.Center;
            var position = center + box.Size.Length * Vector3f.One;
            LookAt(position, center);
        }

        public void SerializeInBlock(Archive archive)
        {
            ArchiveSerialization.SerializeOptionalValue(
                archive, "Position", ref LastCameraPosition, new Vector3f(), ArchiveSerialization.SerializeValue);
            ArchiveSerialization.SerializeOptionalValue(
                archive, "Rotation", ref LastCameraRotation, new Quaternion(), ArchiveSerialization.SerializeValue);

            if (archive.IsInput)
            {
                Yaw = LastCameraRotation.YawAngle;
                Pitch = LastCameraRotation.PitchAngle;
            }
        }
    }

    public CameraController(HotkeyManager hotkeyManager)
    {
        _hotkeyManager = hotkeyManager;

        hotkeyManager.BindPassiveHotkey(_hotkeyMoveForward);
        hotkeyManager.BindPassiveHotkey(_hotkeyMoveBackward);
        hotkeyManager.BindPassiveHotkey(_hotkeyMoveLeft);
        hotkeyManager.BindPassiveHotkey(_hotkeyMoveRight);
        hotkeyManager.BindPassiveHotkey(_hotkeyMoveUp);
        hotkeyManager.BindPassiveHotkey(_hotkeyMoveDown);

        hotkeyManager.BindPassiveHotkey(_hotkeyMoveAccelerate);
        hotkeyManager.BindPassiveHotkey(_hotkeyLookAround);
        hotkeyManager.BindPassiveHotkey(_hotkeyOrbitAround);
    }

    public void ProcessInput(Camera camera, ref PageState state, Settings? settings = null)
    {
        if (!settings.HasValue)
        {
            var settingsManager = GetSubsystem<EditorProject>().SettingsManager;
            var settingsPage = settingsManager.FindPage("Editor.Scene:Camera") as SettingsPage;
            if (settingsPage is not null)
                settings = settingsPage.Values;
        }
        if (!settings.HasValue)
            return;

        var config = settings.GetValueOrDefault();
        camera.SetOrthographic(config.Orthographic);
        if (config.Orthographic)
        {
            var aspectRatio = camera.GetAspectRatio();
            camera.SetOrthoSize(config.OrthoSize);
            camera.SetAspectRatioInternal(aspectRatio);
        }

        var systemUI = GetSubsystem<DearImGuiSubsystem>();

        var wasActive = _isActive;
        _isActive = (wasActive || ImGui.IsItemHovered()) && _hotkeyManager.IsHotkeyActive(_hotkeyLookAround);
        if (_isActive)
        {
            if (!wasActive)
                systemUI.SetRelativeMouseMove(true, true);
        }
        else if (wasActive)
        {
            systemUI.SetRelativeMouseMove(false, true);
        }

        UpdateState(in config, camera, ref state);
    }

    private void UpdateState(in Settings config, Camera camera, ref PageState state)
    {
        var time = GetSubsystem<Time>();

        var node = camera.Node;

        // Restore camera to previous step if moved
        if (state.LastCameraPosition != node.GetPosition())
            node.SetPosition(state.LastCameraPosition);
        if (state.LastCameraRotation != node.GetRotation())
            node.SetRotation(state.LastCameraRotation);

        var isAccelerated = _hotkeyManager.IsHotkeyActive(_hotkeyMoveAccelerate);
        var isOrbiting = _hotkeyManager.IsHotkeyActive(_hotkeyOrbitAround);
        if (_isActive && !isOrbiting)
        {
            // Apply mouse movement
            var mouseMove = GetMouseMove() * config.MouseSensitivity;
            state.Yaw = MathDefs.Mod(state.Yaw + mouseMove.X, 360.0f);
            state.Pitch = MathDefs.Clamp(state.Pitch + mouseMove.Y, -89.0f, 89.0f);

            node.SetRotation(new Quaternion(state.Pitch, state.Yaw, 0.0f));

            // Apply camera movement
            var timeStep = GetSubsystem<Time>().GetTimeStep();
            var moveDirection = GetMoveDirection();
            var multiplier = isAccelerated ? config.ShiftFactor : 1.0f;
            if (moveDirection == Vector3f.Zero)
                state.CurrentMoveSpeed = config.MinSpeed;

            node.Translate(moveDirection * state.CurrentMoveSpeed * multiplier * timeStep);

            // Apply acceleration
            state.CurrentMoveSpeed = MathF.Min(
                config.MaxSpeed, state.CurrentMoveSpeed + config.Acceleration * timeStep);
        }
        else
        {
            state.CurrentMoveSpeed = config.MinSpeed;
        }

        if (isOrbiting)
        {
            if (!state.OrbitPosition.HasValue)
            {
                state.OrbitPosition = node.GetPosition()
                    + node.GetRotation() * new Vector3f(0.0f, 0.0f, config.FocusDistance);
            }

            var mouseMove = GetMouseMove() * config.MouseSensitivity;
            state.Yaw = MathDefs.Mod(state.Yaw + mouseMove.X, 360.0f);
            state.Pitch = MathDefs.Clamp(state.Pitch + mouseMove.Y, -89.0f, 89.0f);

            node.SetRotation(new Quaternion(state.Pitch, state.Yaw, 0.0f));
            state.LastCameraRotation = node.GetRotation();

            node.SetPosition(state.OrbitPosition.Value
                - node.GetRotation() * new Vector3f(0.0f, 0.0f, config.FocusDistance));
            state.LastCameraPosition = node.GetPosition();
        }
        else
        {
            state.OrbitPosition = null;
        }

        if (ImGui.IsItemHovered() && MathF.Abs(ImGui.GetIO().MouseWheel) > 0.05f)
        {
            state.PendingOffset += node.GetWorldDirection() * config.ScrollSpeed
                * MathDefs.Sign(ImGui.GetIO().MouseWheel);
        }

        if (state.PendingOffset.Length > 0.05f)
        {
            var factor = MathDefs.InverseExponentialDecay(config.FocusSpeed * time.GetTimeStep());
            node.Translate(state.PendingOffset * factor, TransformSpace.World);
            state.PendingOffset *= 1.0f - factor;
        }

        state.LastCameraRotation = node.GetRotation();
        state.LastCameraPosition = node.GetPosition();
    }

    private Vector2f GetMouseMove()
    {
        var systemUI = GetSubsystem<DearImGuiSubsystem>();
        return systemUI.RelativeMouseMove;
    }

    private Vector3f GetMoveDirection()
    {
        var moveDirection = new Vector3f();
        foreach (var (hotkey, direction) in _keyMapping)
        {
            if (_hotkeyManager.IsHotkeyActive(hotkey))
                moveDirection += direction;
        }
        return moveDirection.Normalized;
    }

    private bool _isActive;
    private readonly HotkeyManager _hotkeyManager;

    private static readonly EditorHotkey _hotkeyMoveForward =
        new EditorHotkey("EditorCamera.MoveForward").Hold(Scancode.W).Hold(MouseButton.Right).MaybeShift();
    private static readonly EditorHotkey _hotkeyMoveBackward =
        new EditorHotkey("EditorCamera.MoveBackward").Hold(Scancode.S).Hold(MouseButton.Right).MaybeShift();
    private static readonly EditorHotkey _hotkeyMoveLeft =
        new EditorHotkey("EditorCamera.MoveLeft").Hold(Scancode.A).Hold(MouseButton.Right).MaybeShift();
    private static readonly EditorHotkey _hotkeyMoveRight =
        new EditorHotkey("EditorCamera.MoveRight").Hold(Scancode.D).Hold(MouseButton.Right).MaybeShift();
    private static readonly EditorHotkey _hotkeyMoveUp =
        new EditorHotkey("EditorCamera.MoveUp").Hold(Scancode.E).Hold(MouseButton.Right).MaybeShift();
    private static readonly EditorHotkey _hotkeyMoveDown =
        new EditorHotkey("EditorCamera.MoveDown").Hold(Scancode.Q).Hold(MouseButton.Right).MaybeShift();

    private static readonly EditorHotkey _hotkeyMoveAccelerate =
        new EditorHotkey("EditorCamera.MoveAccelerate").Hold(Scancode.LeftShift).Hold(MouseButton.Right).MaybeShift();
    private static readonly EditorHotkey _hotkeyLookAround =
        new EditorHotkey("EditorCamera.LookAround").Hold(MouseButton.Right).MaybeShift().MaybeAlt().MaybeCtrl();
    private static readonly EditorHotkey _hotkeyOrbitAround =
        new EditorHotkey("EditorCamera.OrbitAround").Alt().Hold(MouseButton.Right).MaybeShift();

    private static readonly (EditorHotkey Hotkey, Vector3f Direction)[] _keyMapping =
    [
        (_hotkeyMoveForward, Vector3f.Forward),
        (_hotkeyMoveBackward, Vector3f.Back),
        (_hotkeyMoveLeft, Vector3f.Left),
        (_hotkeyMoveRight, Vector3f.Right),
        (_hotkeyMoveUp, Vector3f.Up),
        (_hotkeyMoveDown, Vector3f.Down),
    ];
}
