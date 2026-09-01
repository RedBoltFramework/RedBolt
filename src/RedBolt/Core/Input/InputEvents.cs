// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Input;

public static class InputEvents
{
    /// <summary>Mouse button pressed.</summary>
    public struct MouseButtonDownEvent
    {
        public MouseButton Button;     // int
        public MouseButton Buttons;    // int
        public Qualifier Qualifiers;   // int
        public int Clicks;             // int
    }

    /// <summary>Mouse button released.</summary>
    public struct MouseButtonUpEvent
    {
        public MouseButton Button;     // int
        public MouseButton Buttons;    // int
        public Qualifier Qualifiers;   // int
    }

    /// <summary>Mouse moved.</summary>
    public struct MouseMoveEvent
    {
        public int X;                  // int (only when mouse visible)
        public int Y;                  // int (only when mouse visible)
        public int DX;                 // int
        public int DY;                 // int
        public MouseButton Buttons;    // int
        public Qualifier Qualifiers;   // int
    }

    /// <summary>Mouse wheel moved.</summary>
    public struct MouseWheelEvent
    {
        public int Wheel;              // int
        public MouseButton Buttons;    // int
        public Qualifier Qualifiers;   // int
    }

    /// <summary>Key pressed.</summary>
    public struct KeyDownEvent
    {
        public Key Key;                // int
        public Scancode Scancode;      // int
        public MouseButton Buttons;    // int
        public Qualifier Qualifiers;   // int
        public bool Repeat;            // bool
    }

    /// <summary>Key released.</summary>
    public struct KeyUpEvent
    {
        public Key Key;                // int
        public Scancode Scancode;      // int
        public MouseButton Buttons;    // int
        public Qualifier Qualifiers;   // int
    }

    /// <summary>Text input event.</summary>
    public struct TextInputEvent
    {
        public string Text;            // String
    }

    /// <summary>Text editing event.</summary>
    public struct TextEditingEvent
    {
        public string Composition;     // String
        public int Cursor;             // int
        public int SelectionLength;    // int
    }

    /// <summary>Joystick connected.</summary>
    public struct JoystickConnectedEvent
    {
        public int JoystickID;         // int
    }

    /// <summary>Joystick disconnected.</summary>
    public struct JoystickDisconnectedEvent
    {
        public int JoystickID;         // int
    }

    /// <summary>Joystick button pressed.</summary>
    public struct JoystickButtonDownEvent
    {
        public int JoystickID;         // int
        public int Button;             // int
    }

    /// <summary>Joystick button released.</summary>
    public struct JoystickButtonUpEvent
    {
        public int JoystickID;         // int
        public int Button;             // int
    }

    /// <summary>Joystick axis moved.</summary>
    public struct JoystickAxisMoveEvent
    {
        public int JoystickID;         // int
        public int Axis;               // int
        public float Position;         // float
    }

    /// <summary>Joystick POV hat moved.</summary>
    public struct JoystickHatMoveEvent
    {
        public int JoystickID;         // int
        public int Hat;                // int
        public HatPosition Position;   // int
    }

    /// <summary>Finger pressed on the screen.</summary>
    public struct TouchBeginEvent
    {
        public int TouchID;            // int
        public int X;                  // int
        public int Y;                  // int
        public float Pressure;         // float
    }

    /// <summary>Finger released from the screen.</summary>
    public struct TouchEndEvent
    {
        public int TouchID;            // int
        public int X;                  // int
        public int Y;                  // int
    }

    /// <summary>Finger moved on the screen.</summary>
    public struct TouchMoveEvent
    {
        public int TouchID;            // int
        public int X;                  // int
        public int Y;                  // int
        public int DX;                 // int
        public int DY;                 // int
        public float Pressure;         // float
    }

    /// <summary>A touch gesture finished recording.</summary>
    public struct GestureRecordedEvent
    {
        public uint GestureID;         // unsigned
    }

    /// <summary>A recognized touch gesture was input by the user.</summary>
    public struct GestureInputEvent
    {
        public uint GestureID;         // unsigned
        public int CenterX;            // int
        public int CenterY;            // int
        public int NumFingers;         // int
        public float Error;            // float
    }

    /// <summary>Pinch/rotate multi-finger touch gesture motion update.</summary>
    public struct MultiGestureEvent
    {
        public int CenterX;            // int
        public int CenterY;            // int
        public int NumFingers;         // int
        public float DTheta;           // float (degrees)
        public float DDist;            // float
    }

    /// <summary>A file was drag-dropped into the application window.</summary>
    public struct DropFileEvent
    {
        public string FileName;        // String
    }

    /// <summary>Application input focus or minimization changed.</summary>
    public struct InputFocusEvent
    {
        public bool Focus;             // bool
        public bool Minimized;         // bool
    }

    /// <summary>Active input device changed.</summary>
    public struct InputDeviceChangedEvent
    {
        public InputDeviceType PreviousDeviceType;
        public InputDeviceType DeviceType;
    }

    /// <summary>OS mouse cursor visibility changed.</summary>
    public struct MouseVisibleChangedEvent
    {
        public bool Visible;           // bool
    }

    /// <summary>Mouse mode changed.</summary>
    public struct MouseModeChangedEvent
    {
        public MouseMode Mode;         // MouseMode
        public bool MouseLocked;       // bool
    }

    /// <summary>Application exit requested.</summary>
    public struct ExitRequestedEvent
    {
    }

    /// <summary>Raw SDL input event.</summary>
    public struct SDLRawInputEvent
    {
        public nint SDLEvent;          // SDL_Event*
        public bool Consumed;          // bool
    }

    /// <summary>Input handling begins.</summary>
    public struct InputBeginEvent
    {
    }

    /// <summary>Input handling ends.</summary>
    public struct InputEndEvent
    {
    }

    /// <summary>Multitouch event from MultitouchAdapter.</summary>
    public struct MultitouchEvent
    {
        public int NumFingers;         // int
        public int EventType;          // int
        public int X;                  // int
        public int Y;                  // int
        public int DX;                 // int
        public int DY;                 // int
        public Vector2i Size;          // IntVector2
        public Vector2i DSize;         // IntVector2
    }
}
