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

using System.Runtime.InteropServices;
using System.Text;

using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.DearImGui;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.IO;
using RedBolt.Logging;
using RedBolt.Math;

/// <summary>Console window with log history and command line prompt.</summary>
public sealed class Console : RedObject
{
    private const int HistoryCapacity = 2000;
    private const int InputBufferSize = 0x1000;

    private static readonly string[] DebugLevelAbbreviations = ["T", "D", "I", "W", "E"];
    private static readonly Vector4f[] LogLevelColors =
    [
        new(0.529f, 0.529f, 0.529f, 1.0f),
        new(0.784f, 0.784f, 0.784f, 1.0f),
        new(1.0f, 1.0f, 1.0f, 1.0f),
        new(0.969f, 0.969f, 0.659f, 1.0f),
        new(0.969f, 0.659f, 0.659f, 1.0f),
        new(1.0f, 1.0f, 1.0f, 1.0f),
    ];

    /// <summary>Construct.</summary>
    public Console()
    {
        _inputBuffer.Resize(InputBufferSize - 1);
        _inputBuffer.Clear();

        SetNumHistoryRows(_historyRows);
        var dummy = new GraphicsEvents.ScreenModeEvent();
        HandleScreenMode(ref dummy);
        RefreshInterpreters();

        SubscribeToEvent<GraphicsEvents.ScreenModeEvent>(HandleScreenMode);
        SubscribeToEvent<IOEvents.LogMessage>(HandleLogMessage);
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        UnsubscribeFromAllEvents();
        Clear();
        _interpreterItems.Dispose();
        _inputBuffer.Dispose();
        base.Dispose();
    }

    /// <summary>Show or hide.</summary>
    public void SetVisible(bool enable)
    {
        _isOpen = enable;
        if (_isOpen)
        {
            _focusInput = true;
            SubscribeToEvent<CoreEvents.UpdateEvent>(RenderUi);
        }
        else
        {
            UnsubscribeFromEvent<CoreEvents.UpdateEvent>();
            ImGui.FocusWindow(null!);
        }
    }

    /// <summary>Toggle visibility.</summary>
    public void Toggle()
    {
        SetVisible(!IsVisible());
    }

    /// <summary>Automatically set console to visible when receiving an error log message.</summary>
    public void SetAutoVisibleOnError(bool enable) => _autoVisibleOnError = enable;

    /// <summary>Set the command interpreter.</summary>
    public void SetCommandInterpreter(string interpreter)
    {
        RefreshInterpreters();

        var index = _interpreters.IndexOf(interpreter);
        if (index == -1)
            index = 0;
        _currentInterpreter = index;
    }

    /// <summary>Set command history maximum size, 0 disables history.</summary>
    public void SetNumHistoryRows(uint rows)
    {
        _historyRows = rows;
        if ((uint)_history.Count > rows)
        {
            var newCount = (int)rows;
            for (var i = newCount; i < _history.Count; ++i)
                _history[i].Dispose();
            _history.RemoveRange(newCount, _history.Count - newCount);
        }
    }

    /// <summary>Set console height.</summary>
    public void SetConsoleHeight(uint height)
    {
        _windowSize.Y = unchecked((int)height);
    }

    /// <summary>Return whether is visible.</summary>
    public bool IsVisible() => _isOpen;

    /// <summary>Return true when console is set to automatically visible when receiving an error log message.</summary>
    public bool IsAutoVisibleOnError() => _autoVisibleOnError;

    /// <summary>Return the last used command interpreter.</summary>
    public string GetCommandInterpreter() => _interpreters[_currentInterpreter];

    /// <summary>Return history maximum size.</summary>
    public uint GetNumHistoryRows() => _historyRows;

    /// <summary>Remove all rows.</summary>
    public void Clear()
    {
        foreach (var entry in _history)
            entry.Dispose();
        _history.Clear();
    }

    /// <summary>Render contents of the console window. Useful for embedding console into custom UI.</summary>
    public unsafe void RenderContent()
    {
        // Force more verbose logging if it is required by the Console.
        var log = GetSubsystem<Log>();
        for (var level = (int)LogLevel.Trace; level < (int)log.Level; ++level)
        {
            if (_levelVisible[level])
            {
                log.SetLevel((LogLevel)level);
                break;
            }
        }

        var region = ImGui.GetContentRegionAvail();
        var showCommandInput = _interpreters.Count != 0;
        var copying = (IsKeyDown(Key.LeftCtrl) || IsKeyDown(Key.RightCtrl)) && IsKeyPressed(Key.C);
        if (ImGui.BeginChild(
                "ConsoleScrollArea"u8,
                new Vector2f(region.X, region.Y - (showCommandInput ? 30.0f : 0.0f)),
                window_flags: ImGuiWindowFlags.ImGuiWindowFlagsHorizontalScrollbar))
        {
            var io = ImGui.GetIO();
            var style = ImGui.GetStyle();
            var textStart = 0;
            foreach (var row in _history)
            {
                var rowText = row.Message.ContentBytes;
                var textEnd = textStart + rowText.Length;

                if (!_levelVisible[(int)row.Level])
                {
                    textStart = textEnd;
                    continue;
                }

                if (_loggersHidden.Contains(row.Logger))
                {
                    textStart = textEnd;
                    continue;
                }

                var rowSize = ImGui.CalcTextSize(rowText);
                var rowStartPos = ImGui.GetCursorScreenPos();
                var rowRect = new Rect(rowStartPos, rowStartPos + rowSize);
                rowRect.Max.Y += style.ItemSpacing.Y; // So clicking between rows still does a selection
                var mousePosition = ImGui.GetMousePos();
                var isRowHovered = Contains(rowRect, mousePosition) && ImGui.IsWindowHovered();

                // Perform selection
                if (ImGui.IsMouseDown(ImGuiMouseButton.ImGuiMouseButtonLeft))
                {
                    if (isRowHovered)
                    {
                        var pos = rowStartPos;
                        var byteOffset = 0;
                        while (byteOffset < rowText.Length)
                        {
                            var remainingText = row.Message.Bytes[byteOffset..];
                            var numBytes = ImGui.ImTextCountUtf8BytesFromChar(remainingText, default);
                            var charSize = ImGui.CalcTextSize(rowText.Slice(byteOffset, numBytes));
                            if (Contains(new Rect(pos, pos + charSize), mousePosition))
                                break;
                            pos.X += charSize.X;
                            byteOffset += numBytes;
                        }
                        _selection.Y = textStart + byteOffset;
                        if (ImGui.IsMouseClicked(ImGuiMouseButton.ImGuiMouseButtonLeft))
                            _selection.X = _selection.Y;
                    }
                }

                // Render selection.
                var selectedStartOffset = System.Math.Clamp(
                    System.Math.Min(_selection.X, _selection.Y) - textStart, 0, rowText.Length);
                var selectedEndOffset = System.Math.Clamp(
                    System.Math.Max(_selection.X, _selection.Y) - textStart, 0, rowText.Length);
                if (selectedStartOffset < selectedEndOffset)
                {
                    if (copying && ImGui.IsWindowFocused())
                    {
                        for (var i = selectedStartOffset; i < selectedEndOffset; ++i)
                            _copyBuffer.Add(rowText[i]);
                        if (selectedEndOffset == rowText.Length
                            && System.Math.Max(_selection.X, _selection.Y) > textEnd)
                        {
                            // Append new line if this line is selected to the end and selection on the next line exists.
#if REDBOLT_PLATFORM_WINDOWS
                            _copyBuffer.Add((byte)'\r');
#endif
                            _copyBuffer.Add((byte)'\n');
                        }
                    }
                    var sizeUnselected = ImGui.CalcTextSize(rowText[..selectedStartOffset]);
                    var sizeSelected = ImGui.CalcTextSize(rowText[selectedStartOffset..selectedEndOffset]);
                    var selection = new Rect(
                        rowStartPos + new Vector2f(sizeUnselected.X, 0.0f),
                        rowStartPos + new Vector2f(sizeUnselected.X + sizeSelected.X, sizeSelected.Y));
                    selection.Max.Y += style.ItemSpacing.Y; // Fill in spaces between lines
                    var drawList = ImGui.GetWindowDrawList();
                    drawList.AddRectFilled(
                        selection.Min, selection.Max, ImGui.GetColorU32(ImGuiCol.ImGuiColTextSelectedBg));
                }
                ImGui.PushStyleColor(ImGuiCol.ImGuiColText, LogLevelColors[(int)row.Level]);
                ImGui.TextUnformatted(rowText);

                // Find URIs, render underlines and send click events
                if (isRowHovered)
                {
                    for (var i = 0; i < rowText.Length;)
                    {
                        var relativeUriPosition = rowText[i..].IndexOf("://"u8);
                        if (relativeUriPosition == -1)
                            break;

                        var uriPosition = i + relativeUriPosition;
                        var uriTerminator = (byte)' ';
                        var uriStart = uriPosition - 1;
                        var uriEnd = uriPosition + 2;
                        while (uriStart > 0 && IsAlphaNumeric(rowText[uriStart]))
                            --uriStart;

                        if (rowText[uriStart] is (byte)'\'' or (byte)'"')
                            uriTerminator = rowText[uriStart];
                        ++uriStart;

                        while (uriEnd < rowText.Length && rowText[uriEnd] != uriTerminator)
                            ++uriEnd;

                        var uriRect = new Rect();
                        uriRect.Min = rowStartPos;
                        uriRect.Min.X += ImGui.CalcTextSize(rowText[..uriStart]).X;
                        uriRect.Max = uriRect.Min + ImGui.CalcTextSize(rowText[uriStart..uriEnd]);

                        if (Contains(uriRect, mousePosition))
                        {
                            var drawList = ImGui.GetWindowDrawList();
                            drawList.AddLine(
                                new Vector2f(uriRect.Min.X, uriRect.Max.Y),
                                uriRect.Max,
                                ImGui.GetColorU32(ImGuiCol.ImGuiColText));
                            ImGui.SetMouseCursor(ImGuiMouseCursor.ImGuiMouseCursorHand);
                            if (ImGui.IsMouseClicked(ImGuiMouseButton.ImGuiMouseButtonLeft)
                                || ImGui.IsMouseClicked(ImGuiMouseButton.ImGuiMouseButtonRight))
                            {
                                var eventData = new EngineEvents.ConsoleUriClickEvent
                                {
                                    Protocol = Encoding.UTF8.GetString(rowText[uriStart..uriPosition]),
                                    Address = Encoding.UTF8.GetString(rowText[(uriPosition + 3)..uriEnd]),
                                };
                                SendEvent(ref eventData);
                            }
                        }

                        i = uriEnd;
                    }
                }
                ImGui.PopStyleColor();
                textStart = textEnd;
            }

            if (_scrollToEnd > 0)
            {
                ImGui.SetScrollHereY(1.0f);
                --_scrollToEnd;
            }

            const float eps = 10.0f;
            _isAtEnd = ImGui.GetScrollY() + eps >= ImGui.GetScrollMaxY();

            if (_copyBuffer.Count != 0)
            {
                ImGui.SetClipboardText(CollectionsMarshal.AsSpan(_copyBuffer));
                _copyBuffer.Clear();
            }

            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 1.0f);
            // Dear ImGui 1.92 requires an item to validate cursor-based content extension.
            ImGui.Dummy(new Vector2f(0.0f, 0.0f));
        }
        ImGui.EndChild();

        if (showCommandInput)
        {
            ImGui.PushItemWidth(110.0f);
            if (ImGui.Combo(
                    "##ConsoleInterpreter"u8,
                    ref _currentInterpreter,
                    _interpreterItems.Bytes))
            {
            }
            ImGui.PopItemWidth();
            ImGui.SameLine();
            ImGui.PushItemWidth(region.X - 120.0f);
            if (_focusInput)
            {
                ImGui.SetKeyboardFocusHere();
                _focusInput = false;
            }
            if (ImGui.InputText(
                    "##ConsoleInput"u8,
                    _inputBuffer.Pointer,
                    InputBufferSize,
                    ImGuiInputTextFlags.ImGuiInputTextFlagsEnterReturnsTrue))
            {
                _focusInput = true;
                var inputBytes = new ReadOnlySpan<byte>((void*)_inputBuffer.Pointer, InputBufferSize);
                var inputLength = inputBytes.IndexOf((byte)0);
                if (inputLength == -1)
                    inputLength = inputBytes.Length;
                var line = Encoding.UTF8.GetString(inputBytes[..inputLength]);
                if (line.Length != 0
                    && _currentInterpreter >= 0 && _currentInterpreter < _interpreters.Count)
                {
                    // Store to history, then clear the lineedit
                    Log.GetLogger().Write(LogLevel.Info, $"> {line}");
                    if ((uint)_history.Count > _historyRows)
                        PopHistoryFront();
                    ScrollToEnd();
                    _inputBuffer.Clear();

                    // Send the command as an event for script subsystem
                    var eventData = new EngineEvents.ConsoleCommandEvent
                    {
                        Command = line,
                        Id = _interpreters[_currentInterpreter],
                    };
                    SendEvent(ref eventData);
                }
            }
            ImGui.PopItemWidth();
        }
    }

    /// <summary>Populate the command line interpreters that could handle the console command.</summary>
    public void RefreshInterpreters()
    {
        _interpreters.Clear();
        _interpreterItems.Clear();

        var group = Context.GetEventReceivers<EngineEvents.ConsoleCommandEvent>();
        if (group is null || group.Count == 0)
            return;

        var currentInterpreterName = string.Empty;
        if (_currentInterpreter >= 0 && _currentInterpreter < _interpreters.Count)
            currentInterpreterName = _interpreters[_currentInterpreter];

        for (var i = 0; i < group.Count; ++i)
        {
            var receiver = group[i];
            if (receiver is not null)
                _interpreters.Add(receiver.GetTypeName());
        }
        _interpreters.Sort(StringComparer.Ordinal);

        RefreshInterpreterItems();

        _currentInterpreter = _interpreters.IndexOf(currentInterpreterName);
        if (_currentInterpreter == -1)
            _currentInterpreter = 0;
    }

    /// <summary>Returns a set of loggers that exist in console history.</summary>
    public List<string> GetLoggers()
    {
        var loggers = new HashSet<string>(StringComparer.Ordinal);
        var loggersVector = new List<string>();

        foreach (var row in _history)
            loggers.Add(row.Logger);

        foreach (var logger in loggers)
            loggersVector.Add(logger);

        loggersVector.Sort(StringComparer.Ordinal);
        return loggersVector;
    }

    /// <summary>Set visibility of certain loggers in the console.</summary>
    public void SetLoggerVisible(string loggerName, bool visible)
    {
        if (_isAtEnd)
            ScrollToEnd();
        if (visible)
            _loggersHidden.Remove(loggerName);
        else
            _loggersHidden.Add(loggerName);
    }

    /// <summary>Get visibility of certain loggers in the console.</summary>
    public bool GetLoggerVisible(string loggerName) => !_loggersHidden.Contains(loggerName);

    /// <summary>Set visibility of certain log levels in the console.</summary>
    public void SetLevelVisible(LogLevel level, bool visible)
    {
        if (level < LogLevel.Trace || level >= LogLevel.None)
            return;

        if (_isAtEnd)
            ScrollToEnd();
        _levelVisible[(int)level] = visible;
    }

    /// <summary>Get visibility of certain log levels in the console.</summary>
    public bool GetLevelVisible(LogLevel level)
    {
        if (level < LogLevel.Trace || level >= LogLevel.None)
            return false;

        return _levelVisible[(int)level];
    }

    /// <summary>Update console size on application window changes.</summary>
    private void HandleScreenMode(ref GraphicsEvents.ScreenModeEvent eventData)
    {
        var graphics = GetSubsystem<Graphics>();
        _windowSize.X = System.Math.Clamp(_windowSize.X, 0, graphics.Width);
        _windowSize.Y = System.Math.Clamp(_windowSize.Y, 0, graphics.Height);
    }

    /// <summary>Handle a log message.</summary>
    private void HandleLogMessage(ref IOEvents.LogMessage eventData)
    {
        var level = (LogLevel)eventData.Level;
        var timestamp = eventData.Time;
        var logger = eventData.Logger ?? string.Empty;
        var message = eventData.Message ?? string.Empty;

        // The message may be multi-line, so split to rows in that case
        var rows = message.Split('\n');
        foreach (var row in rows)
        {
            var formattedMessage = new CString();
            formattedMessage.FormatInPlace(
                $"[{Time.GetTimeStamp(timestamp, "HH:mm:ss")}] [{DebugLevelAbbreviations[(int)level]}] [{logger}] : {row}");
            PushHistory(new LogEntry(level, timestamp, logger, formattedMessage));
        }
        if (_isAtEnd)
            ScrollToEnd();

        if (_autoVisibleOnError && level == LogLevel.Error && !IsVisible())
            SetVisible(true);
    }

    /// <summary>Render system ui.</summary>
    private void RenderUi(ref CoreEvents.UpdateEvent eventData)
    {
        var graphics = GetSubsystem<Graphics>();
        ImGui.SetNextWindowPos(new Vector2f(0.0f, 0.0f));
        var wasOpen = _isOpen;
        var size = new Vector2f(graphics.Width, _windowSize.Y);
        ImGui.SetNextWindowSize(size);

        ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarWindowRounding, 0.0f);
        if (ImGui.Begin(
                "Debug Console"u8,
                ref _isOpen,
                ImGuiWindowFlags.ImGuiWindowFlagsNoTitleBar
                | ImGuiWindowFlags.ImGuiWindowFlagsNoMove
                | ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            RenderContent();
        }
        else if (wasOpen)
        {
            SetVisible(false);
            ImGui.FocusWindow(null!);
            SendEvent<DearImGuiEvents.ConsoleClosedEvent>();
        }

        _windowSize.Y = (int)ImGui.GetWindowHeight();

        ImGui.End();

        ImGui.PopStyleVar();
    }

    /// <summary>Scroll console to the end.</summary>
    private void ScrollToEnd() => _scrollToEnd = 2;

    private void RefreshInterpreterItems()
    {
        var byteLength = 1;
        foreach (var interpreter in _interpreters)
            byteLength += Encoding.UTF8.GetByteCount(interpreter) + 1;

        var items = new byte[byteLength];
        var offset = 0;
        foreach (var interpreter in _interpreters)
        {
            offset += Encoding.UTF8.GetBytes(interpreter.AsSpan(), items.AsSpan(offset));
            ++offset;
        }
        _interpreterItems.Set(items);
    }

    private void PushHistory(LogEntry entry)
    {
        if (_history.Count == HistoryCapacity)
            PopHistoryFront();
        _history.Add(entry);
    }

    private void PopHistoryFront()
    {
        _history[0].Dispose();
        _history.RemoveAt(0);
    }

    private static bool IsKeyDown(Key key)
    {
        var imguiKey = ImGui.ImGuiImplSDL2KeyEventToImGuiKey((int)key, (int)Input.GetScancodeFromKey(key));
        return ImGui.IsKeyDown(imguiKey);
    }

    private static bool IsKeyPressed(Key key)
    {
        var imguiKey = ImGui.ImGuiImplSDL2KeyEventToImGuiKey((int)key, (int)Input.GetScancodeFromKey(key));
        return ImGui.IsKeyPressed(imguiKey);
    }

    private static bool Contains(in Rect rect, in Vector2f point)
    {
        return point.X >= rect.Min.X && point.Y >= rect.Min.Y
            && point.X < rect.Max.X && point.Y < rect.Max.Y;
    }

    private static bool IsAlphaNumeric(byte value)
    {
        return value is >= (byte)'0' and <= (byte)'9'
            or >= (byte)'A' and <= (byte)'Z'
            or >= (byte)'a' and <= (byte)'z';
    }

    private sealed class LogEntry : IDisposable
    {
        public LogEntry(LogLevel level, uint timestamp, string logger, CString message)
        {
            Level = level;
            Timestamp = timestamp;
            Logger = logger;
            Message = message;
        }

        /// <summary>Log level.</summary>
        public LogLevel Level { get; }

        /// <summary>Time when event was logged.</summary>
        public uint Timestamp { get; }

        /// <summary>Name of logger.</summary>
        public string Logger { get; }

        /// <summary>Log message.</summary>
        public CString Message { get; }

        public void Dispose() => Message.Dispose();
    }

    /// <summary>Auto visible on error flag.</summary>
    private bool _autoVisibleOnError;

    /// <summary>List of command interpreters.</summary>
    private readonly List<string> _interpreters = [];

    /// <summary>Zero-separated strings in interpreters list for efficient UI rendering.</summary>
    private readonly CString _interpreterItems = new();

    /// <summary>Last used command interpreter.</summary>
    private int _currentInterpreter;

    /// <summary>Command history.</summary>
    private readonly List<LogEntry> _history = new(HistoryCapacity);

    /// <summary>Command history maximum rows.</summary>
    private uint _historyRows = 512;

    /// <summary>Is console window open.</summary>
    private bool _isOpen;

    /// <summary>Input box buffer.</summary>
    private readonly CString _inputBuffer = new();

    /// <summary>Console window size.</summary>
    private Vector2i _windowSize = new(int.MaxValue, 200);

    /// <summary>Number of frames to attempt scrolling to the end. Usually two tries are required to properly complete the action (for some reason).</summary>
    private int _scrollToEnd;

    /// <summary>Flag indicating that console input should be focused on the next frame.</summary>
    private bool _focusInput;

    /// <summary>Set of loggers to be omitted from rendering.</summary>
    private readonly HashSet<string> _loggersHidden = new(StringComparer.Ordinal);

    /// <summary>Log level visibility flags.</summary>
    private readonly bool[] _levelVisible =
    [
        false, // LOG_TRACE
#if DEBUG
        true, // LOG_DEBUG
#else
        false, // LOG_DEBUG
#endif
        true, // LOG_INFO
        true, // LOG_WARNING
        true, // LOG_ERROR
    ];

    /// <summary>Current selection in console window. This range denote start and end of selected characters and may span multiple log lines.</summary>
    private Vector2i _selection;

    /// <summary>Temporary variable for accumulating selection in order to copy it to clipboard.</summary>
    private readonly List<byte> _copyBuffer = [];

    /// <summary>When set to true scrollbar of messages panel is at the bottom.</summary>
    private bool _isAtEnd = true;
}
