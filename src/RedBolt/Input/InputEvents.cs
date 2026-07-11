namespace RedBolt;

public static class InputEvents
{
    public struct MouseButtonDownEvent { public MouseButton Button; public MouseButton Buttons; public Qualifier Qualifiers; public int Clicks; }
    public struct MouseButtonUpEvent { public MouseButton Button; public MouseButton Buttons; public Qualifier Qualifiers; }
    public struct MouseMoveEvent { public int X; public int Y; public int DX; public int DY; public MouseButton Buttons; public Qualifier Qualifiers; }
    public struct MouseWheelEvent { public int Wheel; public MouseButton Buttons; public Qualifier Qualifiers; }
    public struct KeyDownEvent { public Key Key; public Scancode Scancode; public MouseButton Buttons; public Qualifier Qualifiers; public bool Repeat; }
    public struct KeyUpEvent { public Key Key; public Scancode Scancode; public MouseButton Buttons; public Qualifier Qualifiers; }
    public struct TextInputEvent { public string Text; }
    public struct TextEditingEvent { public string Composition; public int Cursor; public int SelectionLength; }
    public struct JoystickConnectedEvent { public int JoystickID; }
    public struct JoystickDisconnectedEvent { public int JoystickID; }
    public struct JoystickButtonDownEvent { public int JoystickID; public int Button; }
    public struct JoystickButtonUpEvent { public int JoystickID; public int Button; }
    public struct JoystickAxisMoveEvent { public int JoystickID; public int Axis; public float Position; }
    public struct JoystickHatMoveEvent { public int JoystickID; public int Hat; public HatPosition Position; }
    public struct TouchBeginEvent { public int TouchID; public int X; public int Y; public float Pressure; }
    public struct TouchEndEvent { public int TouchID; public int X; public int Y; }
    public struct TouchMoveEvent { public int TouchID; public int X; public int Y; public int DX; public int DY; public float Pressure; }
    public struct GestureRecordedEvent { public uint GestureID; }
    public struct GestureInputEvent { public uint GestureID; public int CenterX; public int CenterY; public int NumFingers; public float Error; }
    public struct MultiGestureEvent { public int CenterX; public int CenterY; public int NumFingers; public float DTheta; public float DDist; }
    public struct DropFileEvent { public string FileName; }
    public struct InputFocusEvent { public bool Focus; public bool Minimized; }
    public struct MouseVisibleChangedEvent { public bool Visible; }
    public struct MouseModeChangedEvent { public MouseMode Mode; public bool MouseLocked; }
    public struct ExitRequestedEvent { }
    public struct SDLRawInputEvent { public nint SDLEvent; public bool Consumed; }
    public struct InputBeginEvent { }
    public struct InputEndEvent { }
    public struct MultitouchEvent
    {
        public int NumFingers;
        public int EventType;
        public int X;
        public int Y;
        public int DX;
        public int DY;
        public IntVector2 Size;
        public IntVector2 DSize;
    }
}
