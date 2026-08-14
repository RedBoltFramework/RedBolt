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

namespace RedBolt.RmlUI;

using System.Runtime.InteropServices;

using RedBolt.Bindings.RmlUi;
using RedBolt.Bindings.Sdl;
using RedBolt.Container;
using RedBolt.Core;
using Context = RedBolt.Core.Context;
using RedBolt.Input;
using EngineLog = RedBolt.Logging.Log;
using RedBolt.Resource;

public class RmlSystem : SystemInterface
{
    /// <summary>Construct.</summary>
    public RmlSystem()
    {
    }

    /// <summary>Get the number of seconds elapsed since the start of the application.</summary>
    public override double GetElapsedTime()
    {
        return Context.Instance.GetSubsystem<Time>()!.GetElapsedTime();
    }

    /// <summary>Translate the input string into the translated string.</summary>
    public override int TranslateString(Std.String translated, Std.String input)
    {
        var localization = Context.Instance.GetSubsystem<Localization>()!;
        if (localization.GetLanguageIndex() > -1)
        {
            translated.Assign(localization.Get(input.ToString()));
            return 1;
        }
        translated.Assign(input.ToString());
        return 0;
    }

    /// <summary>Log the specified message.</summary>
    /// <param name="type">Type of log message, ERROR, WARNING, etc.</param>
    /// <param name="message">Message to log.</param>
    /// <returns>True to continue execution, false to break into the debugger.</returns>
    public override bool LogMessage(LogType type, Std.String message)
    {
        switch (type)
        {
        case LogType.LtAlways:
        case LogType.LtError:
        case LogType.LtAssert:
            EngineLog.GetLogger().Error(message.ToString());
            break;
        case LogType.LtWarning:
            EngineLog.GetLogger().Warning(message.ToString());
            break;
        case LogType.LtInfo: // There is nothing worthy of "info" status reported by RmlUI.
        case LogType.LtDebug:
            EngineLog.GetLogger().Debug(message.ToString());
            break;
        default:
            return false;
        }
        return true;
    }

    /// <summary>Set mouse cursor.</summary>
    /// <param name="cursorName">Cursor name to activate.</param>
    public override void SetMouseCursor(Std.String cursorName)
    {
        base.SetMouseCursor(cursorName);
    }

    /// <summary>Set clipboard text.</summary>
    /// <param name="text">Text to apply to clipboard.</param>
    public override void SetClipboardText(Std.String text)
    {
        using var clipboardText = new CString(text.ToString());
        Sdl.SdlSetClipboardText(clipboardText.Bytes);
    }

    /// <summary>Get clipboard text.</summary>
    /// <param name="text">Retrieved text from clipboard.</param>
    public override void GetClipboardText(Std.String text)
    {
        text.Assign(Marshal.PtrToStringUTF8(Sdl.SdlGetClipboardText()));
    }

    /// <summary>Activate keyboard (for touchscreen devices)</summary>
    public override void ActivateKeyboard(Vector2f caretPosition, float lineHeight)
    {
        var input = Context.Instance.GetSubsystem<Input>();
        var time = Context.Instance.GetSubsystem<Time>();
        input!.SetScreenKeyboardVisible(true);
        _textInputActivatedFrame = time!.GetFrameNumber();
    }

    /// <summary>Deactivate keyboard (for touchscreen devices)</summary>
    public override void DeactivateKeyboard()
    {
        var input = Context.Instance.GetSubsystem<Input>();
        input!.SetScreenKeyboardVisible(false);
    }

    /// <summary>Return true if text input activation was requested on this frame.</summary>
    public bool TextInputActivatedThisFrame()
    {
        var time = Context.Instance.GetSubsystem<Time>();
        return _textInputActivatedFrame == time!.GetFrameNumber();
    }

    /// <summary>Frame on which text input was activated. Used for managing input focus between multiple RmlUi contexts.</summary>
    private uint _textInputActivatedFrame;
}
