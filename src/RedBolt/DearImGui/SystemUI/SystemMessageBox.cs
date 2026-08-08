//
// Copyright (c) 2017 the Urho3D project.
// Copyright (c) 2008-2015 the Urho3D project.
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

namespace RedBolt.SystemUI;

using System.Runtime.CompilerServices;

using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.DearImGui;
using RedBolt.Graphics;
using RedBolt.Math;

/// <summary>Message box dialog.</summary>
[Reflected]
public partial class SystemMessageBox : RedObject
{
    /// <summary>Construct with default text.</summary>
    public SystemMessageBox()
        : this(string.Empty, string.Empty)
    {
    }

    /// <summary>Construct.</summary>
    public SystemMessageBox(string messageString, string titleString = "")
    {
        _messageText.Set(messageString);
        _isOpen = true;

        SetTitle(titleString);
        var graphics = GetSubsystem<Graphics>()!;
        _windowSize = new Vector2f(300.0f, 150.0f);
        _windowPosition = new Vector2f(
            graphics.Width * 0.5f - _windowSize.X * 0.5f,
            graphics.Height * 0.5f - _windowSize.Y * 0.5f);
        SubscribeToEvent<CoreEvents.UpdateEvent>(RenderFrame);
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        UnsubscribeFromAllEvents();
        _messageText.Dispose();
        _titleText.Dispose();
        base.Dispose();
    }

    /// <summary>Set title text. No-ops if there is no title text element.</summary>
    public void SetTitle(string text)
    {
        _titleText.FormatInPlace($"{text}##0x{RuntimeHelpers.GetHashCode(this):x}");
    }

    /// <summary>Set message text. No-ops if there is no message text element.</summary>
    public void SetMessage(string text)
    {
        _messageText.Set(text);
    }

    /// <summary>Return title text. Return empty string if there is no title text element.</summary>
    public string GetTitle() => _titleText.ToString();

    /// <summary>Return message text. Return empty string if there is no message text element.</summary>
    public string GetMessage() => _messageText.ToString();

    /// <summary>Return true if message box is open.</summary>
    public bool IsOpen() => _isOpen;

    /// <summary>Render message box UI.</summary>
    private void RenderFrame(ref CoreEvents.UpdateEvent eventData)
    {
        _ = eventData;

        ImGui.SetNextWindowPos(_windowPosition, ImGuiCond.ImGuiCondFirstUseEver);
        ImGui.SetNextWindowSize(_windowSize, ImGuiCond.ImGuiCondAlways);
        if (ImGui.Begin(
                _titleText.ContentBytes,
                ref _isOpen,
                ImGuiWindowFlags.ImGuiWindowFlagsNoCollapse
                    | ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            ImGui.TextUnformatted(_messageText.ContentBytes);
            var region = ImGui.GetContentRegionAvail();
            ImGui.SetCursorPos(new Vector2f(region.X - 100.0f + 20.0f, region.Y + 20.0f));

            var closeWindow = false;
            var status = false;
            if (ImGui.Button("Ok"u8))
            {
                closeWindow = true;
                status = true;
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel"u8) || !_isOpen)
            {
                closeWindow = true;
                status = false;
            }

            if (closeWindow)
                Acknowledge(status);
        }
        ImGui.End();
    }

    private void Acknowledge(bool status)
    {
        var args = new DearImGuiEvents.MessageAckEvent { OK = status };
        SendEvent(ref args);
        _isOpen = false;
    }

    /// <summary>Title text element.</summary>
    private readonly CString _titleText = new();
    /// <summary>Message text element.</summary>
    private readonly CString _messageText = new();
    /// <summary>Is message box window open.</summary>
    private bool _isOpen;
    /// <summary>Initial message box window position.</summary>
    private Vector2f _windowPosition;
    /// <summary>Initial message box window size.</summary>
    private Vector2f _windowSize;

    private bool _disposed;
}
