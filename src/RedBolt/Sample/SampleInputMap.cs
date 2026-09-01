// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Sample;

using System;

using RedBolt.Input;
using RedBolt.Math;

/// <summary>Owned multimodal action map used by standalone samples.</summary>
internal sealed class SampleInputMap : IDisposable
{
    public SampleInputMap(bool movement = false, bool touchExit = true)
    {
        Map = new InputMap();
        Overlay = new InputOverlay(Map);
        Map.MapKeyboardKey("Exit", Scancode.Escape);
        Map.MapControllerButton("Exit", ControllerButton.Back);
        if (touchExit)
        {
            Overlay.AddButton("Exit", "Exit", null, "BACK", Vector2f.Zero, new Vector2f(12.0f, 12.0f),
                InputOverlayAlignment.TopLeft, new Vector2f(52.0f, 40.0f));
        }
        if (movement)
            AddMovement();
    }

    public InputMap Map { get; }
    public InputOverlay Overlay { get; }

    public float Evaluate(string action) => Map.Evaluate(action);
    public bool Pressed(string action) => Map.EvaluatePressed(action);

    public void AddButton(string action, Scancode scancode, ControllerButton controllerButton,
        string label, int index = 0)
    {
        Map.MapKeyboardKey(action, scancode);
        Map.MapControllerButton(action, controllerButton);
        AddTouchButton(action, label, index);
    }

    public void AddMouseButton(string action, MouseButton mouseButton, ControllerButton controllerButton,
        string label, int index = 0)
    {
        Map.MapMouseButton(action, mouseButton);
        Map.MapControllerButton(action, controllerButton);
        AddTouchButton(action, label, index);
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

    private void AddTouchButton(string action, string label, int index)
    {
        var column = index % 2;
        var row = index / 2;
        Overlay.AddButton(action, action, null, label, Vector2f.One,
            new Vector2f(-12.0f - column * 84.0f, -12.0f - row * 84.0f),
            InputOverlayAlignment.BottomRight);
    }

    private void MapDirection(string action, Scancode primary, Scancode alternate,
        ControllerButton dPad, ControllerAxis axis, float pressed)
    {
        Map.MapKeyboardKey(action, primary);
        Map.MapKeyboardKey(action, alternate);
        Map.MapControllerButton(action, dPad);
        Map.MapControllerAxis(action, axis, 0.0f, pressed);
    }
}
