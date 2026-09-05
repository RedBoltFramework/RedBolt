// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Sample;

using System;
using System.Collections.Generic;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;

/// <summary>Owned multimodal action map used by standalone samples.</summary>
internal sealed class SampleInputMap : IDisposable
{
    public SampleInputMap(bool movement = false)
    {
        Map = new InputMap();
        Overlay = new InputOverlay(Map);
        Map.MapKeyboardKey("Exit", Scancode.Escape);
        Map.MapControllerButton("Exit", ControllerButton.Back);
        if (movement)
            AddMovement();
    }

    public InputMap Map { get; }
    public InputOverlay Overlay { get; }

    public float Evaluate(string action)
    {
        Overlay.AttachToPrimaryViewport();
        if (IsTouchButton(action))
            return Overlay.IsButtonDown(action) ? 1.0f : 0.0f;
        return Map.Evaluate(action);
    }

    public bool Pressed(string action)
    {
        Overlay.AttachToPrimaryViewport();
        if (IsTouchButton(action))
            return Overlay.IsButtonPressed(action);
        return Map.EvaluatePressed(action);
    }

    public void AttachToScene(Scene scene, uint viewMask = uint.MaxValue) => Overlay.AttachToScene(scene, viewMask);

    public void AddButton(string action, Scancode scancode, ControllerButton controllerButton,
        string label, int index = 0)
    {
        Map.MapKeyboardKey(action, scancode);
        Map.MapControllerButton(action, controllerButton);
        AddTouchButton(action, controllerButton, label, index);
    }

    public void AddMouseButton(string action, MouseButton mouseButton, ControllerButton controllerButton,
        string label, int index = 0)
    {
        Map.MapMouseButton(action, mouseButton);
        Map.MapControllerButton(action, controllerButton);
        AddTouchButton(action, controllerButton, label, index);
    }

    public void AddMovement()
    {
        MapDirection("Forward", Scancode.W, Scancode.Up, ControllerButton.DPadUp, ControllerAxis.LeftY, -1.0f);
        MapDirection("Back", Scancode.S, Scancode.Down, ControllerButton.DPadDown, ControllerAxis.LeftY, 1.0f);
        MapDirection("Left", Scancode.A, Scancode.Left, ControllerButton.DPadLeft, ControllerAxis.LeftX, -1.0f);
        MapDirection("Right", Scancode.D, Scancode.Right, ControllerButton.DPadRight, ControllerAxis.LeftX, 1.0f);
        Overlay.AddJoystick("Movement", "Forward", "Back", "Left", "Right");
    }

    public void Dispose()
    {
        Overlay.Dispose();
        Map.Dispose();
    }

    internal static Texture2D GetControllerPrompt(ControllerButton button)
    {
        var name = button switch
        {
            ControllerButton.A => "ThirdParty/KenneyInputPrompts/xbox_button_color_a.png",
            ControllerButton.B => "ThirdParty/KenneyInputPrompts/xbox_button_color_b.png",
            ControllerButton.X => "ThirdParty/KenneyInputPrompts/xbox_button_color_x.png",
            ControllerButton.Y => "ThirdParty/KenneyInputPrompts/xbox_button_color_y.png",
            ControllerButton.Back => "ThirdParty/KenneyInputPrompts/xbox_button_back.png",
            ControllerButton.Start => "ThirdParty/KenneyInputPrompts/xbox_button_start.png",
            ControllerButton.LeftShoulder => "ThirdParty/KenneyInputPrompts/xbox_lb.png",
            ControllerButton.RightShoulder => "ThirdParty/KenneyInputPrompts/xbox_rb.png",
            ControllerButton.LeftStick => "ThirdParty/KenneyInputPrompts/xbox_stick_l.png",
            ControllerButton.RightStick => "ThirdParty/KenneyInputPrompts/xbox_stick_r.png",
            _ => "ThirdParty/KenneyInputPrompts/generic_button.png"
        };
        return Context.Instance.GetSubsystem<ResourceCache>()!.Get<Texture2D>(name);
    }

    internal static Texture2D GetMovementPrompt() => Context.Instance.GetSubsystem<Input>()!.ActiveInputDevice switch
    {
        InputDeviceType.Touch => Context.Instance.GetSubsystem<ResourceCache>()!.Get<Texture2D>(
            "ThirdParty/KenneyInputPrompts/touch_swipe_move.png"),
        InputDeviceType.Joystick => GetControllerPrompt(ControllerButton.LeftStick),
        _ => Context.Instance.GetSubsystem<ResourceCache>()!.Get<Texture2D>(
            "ThirdParty/KenneyInputPrompts/keyboard_arrows.png")
    };

    internal static Texture2D GetFreeFlyMovementPrompt() =>
        Context.Instance.GetSubsystem<Input>()!.ActiveInputDevice == InputDeviceType.Touch
            ? Context.Instance.GetSubsystem<ResourceCache>()!.Get<Texture2D>(
                "ThirdParty/KenneyInputPrompts/touch_swipe_two_move.png")
            : GetMovementPrompt();

    internal static ReadOnlySpan<byte> GetFreeFlyMovementLabel() =>
        Context.Instance.GetSubsystem<Input>()!.ActiveInputDevice == InputDeviceType.Touch
            ? "Two-finger drag / pinch to move"u8
            : "Move"u8;

    internal static ReadOnlySpan<byte> GetFreeFlyLookLabel() =>
        Context.Instance.GetSubsystem<Input>()!.ActiveInputDevice == InputDeviceType.Touch
            ? "One-finger drag to look"u8
            : "Look"u8;

    internal static Texture2D GetLookPrompt() => Context.Instance.GetSubsystem<Input>()!.ActiveInputDevice switch
    {
        InputDeviceType.Touch => Context.Instance.GetSubsystem<ResourceCache>()!.Get<Texture2D>(
            "ThirdParty/KenneyInputPrompts/touch_swipe_move.png"),
        InputDeviceType.Joystick => GetControllerPrompt(ControllerButton.RightStick),
        _ => Context.Instance.GetSubsystem<ResourceCache>()!.Get<Texture2D>(
            "ThirdParty/KenneyInputPrompts/mouse_move.png")
    };

    internal static Texture2D GetActionPrompt(Texture2D keyboardMousePrompt, ControllerButton controllerButton) =>
        Context.Instance.GetSubsystem<Input>()!.ActiveInputDevice == InputDeviceType.KeyboardMouse
            ? keyboardMousePrompt
            : GetControllerPrompt(controllerButton);

    private void AddTouchButton(string action, ControllerButton controllerButton, string label, int index)
    {
        _touchButtonActions.Add(action);
        var column = index % 2;
        var row = index / 2;
        Overlay.AddButton(action, action, GetControllerPrompt(controllerButton), label, Vector2f.One,
            new Vector2f(-15.0f - column * 84.0f, -15.0f - row * 84.0f),
            InputOverlayAlignment.BottomRight);
    }

    private bool IsTouchButton(string action) => Context.Instance.GetSubsystem<Input>()!.ActiveInputDevice
        == InputDeviceType.Touch && _touchButtonActions.Contains(action);

    private void MapDirection(string action, Scancode primary, Scancode alternate,
        ControllerButton dPad, ControllerAxis axis, float pressed)
    {
        Map.MapKeyboardKey(action, primary);
        Map.MapKeyboardKey(action, alternate);
        Map.MapControllerButton(action, dPad);
        Map.MapControllerAxis(action, axis, 0.0f, pressed);
    }

    private readonly HashSet<string> _touchButtonActions = new(StringComparer.Ordinal);
}
