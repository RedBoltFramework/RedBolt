namespace RedBolt;

[Flags]
public enum MouseButton : uint
{
    None = 0,
    Left = 1u << 0,
    Middle = 1u << 1,
    Right = 1u << 2,
    X1 = 1u << 3,
    X2 = 1u << 4,
    Any = uint.MaxValue
}

[Flags]
public enum Qualifier : uint
{
    None = 0,
    Shift = 1u << 0,
    Ctrl = 1u << 1,
    Alt = 1u << 2,
    Any = 1u << 3
}

public enum Key : uint
{
    Unknown = 0,
    A = 'a',
    B = 'b',
    C = 'c',
    D = 'd',
    E = 'e',
    F = 'f',
    G = 'g',
    H = 'h',
    I = 'i',
    J = 'j',
    K = 'k',
    L = 'l',
    M = 'm',
    N = 'n',
    O = 'o',
    P = 'p',
    Q = 'q',
    R = 'r',
    S = 's',
    T = 't',
    U = 'u',
    V = 'v',
    W = 'w',
    X = 'x',
    Y = 'y',
    Z = 'z',
    Num0 = '0',
    Num1 = '1',
    Num2 = '2',
    Num3 = '3',
    Num4 = '4',
    Num5 = '5',
    Num6 = '6',
    Num7 = '7',
    Num8 = '8',
    Num9 = '9',
    Backspace = 8,
    Tab = 9,
    Return = 13,
    Escape = 27,
    Space = 32,
    Delete = 127,
    Right = 1073741903,
    Left = 1073741904,
    Down = 1073741905,
    Up = 1073741906,
    PageUp = 1073741899,
    PageDown = 1073741902,
    Home = 1073741898,
    End = 1073741901,
    Insert = 1073741897,
    F1 = 1073741882,
    F2 = 1073741883,
    F3 = 1073741884,
    F4 = 1073741885,
    F5 = 1073741886,
    F6 = 1073741887,
    F7 = 1073741888,
    F8 = 1073741889,
    F9 = 1073741890,
    F10 = 1073741891,
    F11 = 1073741892,
    F12 = 1073741893,
    LeftShift = 1073742049,
    RightShift = 1073742053,
    LeftCtrl = 1073742048,
    RightCtrl = 1073742052,
    LeftAlt = 1073742050,
    RightAlt = 1073742054
}

public enum Scancode : uint
{
    Unknown = 0,
    A = 4,
    B = 5,
    C = 6,
    D = 7,
    E = 8,
    F = 9,
    G = 10,
    H = 11,
    I = 12,
    J = 13,
    K = 14,
    L = 15,
    M = 16,
    N = 17,
    O = 18,
    P = 19,
    Q = 20,
    R = 21,
    S = 22,
    T = 23,
    U = 24,
    V = 25,
    W = 26,
    X = 27,
    Y = 28,
    Z = 29,
    Escape = 41,
    Space = 44,
    Right = 79,
    Left = 80,
    Down = 81,
    Up = 82,
    LeftCtrl = 224,
    LeftShift = 225,
    LeftAlt = 226,
    RightCtrl = 228,
    RightShift = 229,
    RightAlt = 230
}

public enum MouseMode
{
    Absolute,
    Relative,
    Wrap,
    Free,
    Invalid
}

[Flags]
public enum HatPosition : uint
{
    Center = 0,
    Up = 1,
    Right = 2,
    Down = 4,
    Left = 8,
    RightUp = Right | Up,
    RightDown = Right | Down,
    LeftUp = Left | Up,
    LeftDown = Left | Down
}

public enum ControllerButton : uint
{
    A,
    B,
    X,
    Y,
    Back,
    Guide,
    Start,
    LeftStick,
    RightStick,
    LeftShoulder,
    RightShoulder,
    DPadUp,
    DPadDown,
    DPadLeft,
    DPadRight
}

public enum ControllerAxis : uint
{
    LeftX,
    LeftY,
    RightX,
    RightY,
    TriggerLeft,
    TriggerRight
}

public enum JoystickDeviceType : uint
{
    Unknown,
    GameController,
    Wheel,
    ArcadeStick,
    FlightStick,
    DancePad,
    Guitar,
    DrumKit,
    ArcadePad,
    Throttle
}
