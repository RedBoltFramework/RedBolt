// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Sample.AggregatedInput;

using System;
using System.Buffers;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Text;

using RedBolt;
using RedBolt.Bindings.DearImGui;
using RedBolt.DearImGui;
using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Math;
using Widgets = RedBolt.SystemUI.Widgets;

internal static class AggregatedInputProgram
{
    private static int Main()
    {
        try { return new AggregatedInputApplication().Run(); }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}

/// <summary>
/// Aggregated input example to compare raw input events to aggregated events.
/// Demonstrates how
/// - DirectionAggregator evaluates aggregated direction from all input devices.
/// - DirectionalPadAdapter translates input events into simple directional events similar to d-pad.
/// </summary>
sealed class AggregatedInputApplication : Application
{
    private DirectionAggregator? _aggregatedInput;
    private DirectionalPadAdapter? _dpadInput;
    private Texture2D[]? _keyboardPrompts;
    private Texture2D[]? _sourcePrompts;
    private ImFont? _headingFont;
    private Vector2f _direction;
    private readonly byte[] _directionText = new byte[64];
    private int _directionTextLength;
    private readonly List<byte[]> _rawEvents = [];
    private readonly List<byte[]> _filteredEvents = [];

    /// <summary>Construct.</summary>
    public AggregatedInputApplication()
    {
    }

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.AggregatedInput";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.ResourcePaths = "CoreData;SampleData";
        parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowMaximize = false;
#if !REDBOLT_PLATFORM_BROWSER
            parameters.WindowWidth = 1280;
            parameters.WindowHeight = 720;
#endif
        parameters.WindowTitle = "RedBolt Aggregated Input";
    }

    /// <summary>Setup after engine initialization and before running the main loop.</summary>
    protected override void Start()
    {
        _sampleInput = new SampleInputMap();
        _aggregatedInput = new DirectionAggregator();
        _dpadInput = new DirectionalPadAdapter();

        // Create UI elements
        CreateUI();

        // Finally subscribe to events.
        SubscribeToEvents();

        // Set the mouse mode to use in the sample
        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(true);
    }

    /// <summary>Construct a new Text instance, containing the 'Hello World' String, and add it to the UI root element.</summary>
    private void CreateUI()
    {
        var dearImGui = new DearImGui((ImGuiConfigFlags)Engine.Parameters.DearImGuiFlags);
        Context.RegisterSubsystem(dearImGui);
        _headingFont = dearImGui.AddFont("Fonts/Anonymous Pro.ttf", size: 28.0f);
        _keyboardPrompts =
        [
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_w.png"),
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_a.png"),
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_s.png"),
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_d.png")
        ];
        _sourcePrompts =
        [
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_arrows.png"),
            SampleInputMap.GetControllerPrompt(ControllerButton.LeftStick),
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/touch_swipe_move.png")
        ];
        SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>(
            (ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => Draw());
    }

    /// <summary>Subscribe to application-wide logic update events.</summary>
    private void SubscribeToEvents()
    {
        _aggregatedInput!.SetEnabled(true);
        _dpadInput!.SetEnabled(true);
        SubscribeToEvent<InputEvents.KeyUpEvent>(_dpadInput, HandleDPadKeyUp);
        SubscribeToEvent<InputEvents.KeyDownEvent>(_dpadInput, HandleDPadKeyDown);
        SubscribeToEvent<InputEvents.KeyUpEvent>(Input, HandleKeyUp);
        SubscribeToEvent<InputEvents.KeyDownEvent>(Input, HandleKeyDown);
        SubscribeToEvent<InputEvents.JoystickAxisMoveEvent>(Input, HandleJoystickAxisMove);
        SubscribeToEvent<InputEvents.JoystickHatMoveEvent>(Input, HandleJoystickHatMove);
        SubscribeToEvent<InputEvents.JoystickDisconnectedEvent>(Input, HandleJoystickDisconnected);
        SubscribeToEvent<InputEvents.TouchBeginEvent>(Input, HandleTouchBegin);
        SubscribeToEvent<InputEvents.TouchMoveEvent>(Input, HandleTouchMove);
        SubscribeToEvent<InputEvents.TouchEndEvent>(Input, HandleTouchEnd);
    }

    private void Deactivate()
    {
        _aggregatedInput!.SetEnabled(false);
        _dpadInput!.SetEnabled(false);
    }

    private void AddFilteredEvent(string str)
    {
        _filteredEvents.Add(Encoding.UTF8.GetBytes(str));
        if (_filteredEvents.Count > 32)
            _filteredEvents.RemoveAt(0);
    }

    private void AddRawEvent(string str)
    {
        _rawEvents.Add(Encoding.UTF8.GetBytes(str));
        if (_rawEvents.Count > 32)
            _rawEvents.RemoveAt(0);
    }

    private void HandleDPadKeyDown(ref InputEvents.KeyDownEvent value)
    {
        AddFilteredEvent($"KeyDown: Key {Input.GetKeyName(value.Key)}, Scancode {Input.GetScancodeName(value.Scancode)}" +
            (value.Repeat ? ", R" : ""));
    }

    private void HandleDPadKeyUp(ref InputEvents.KeyUpEvent value)
    {
        AddFilteredEvent($"KeyUp: Key {Input.GetKeyName(value.Key)}, Scancode {Input.GetScancodeName(value.Scancode)}");
    }

    private void HandleKeyDown(ref InputEvents.KeyDownEvent value)
    {
        if (value.Repeat)
            return;

        AddRawEvent($"KeyDown: Key {Input.GetKeyName(value.Key)}, Scancode {Input.GetScancodeName(value.Scancode)}");
    }

    private void HandleKeyUp(ref InputEvents.KeyUpEvent value)
    {
        AddRawEvent($"KeyUp: Key {Input.GetKeyName(value.Key)}, Scancode {Input.GetScancodeName(value.Scancode)}");
    }

    private void HandleJoystickAxisMove(ref InputEvents.JoystickAxisMoveEvent value)
    {
        AddRawEvent($"AxisMove: Axis {value.Axis}, Value {value.Position}");
    }

    private void HandleJoystickHatMove(ref InputEvents.JoystickHatMoveEvent value)
    {
        var positionName = value.Position switch
        {
            HatPosition.Right => "Right",
            HatPosition.Left => "Left",
            HatPosition.Up => "Up",
            HatPosition.Down => "Down",
            HatPosition.Center => "Center",
            HatPosition.RightDown => "Right Down",
            HatPosition.LeftDown => "Left Down",
            HatPosition.RightUp => "Right Up",
            HatPosition.LeftUp => "Left Up",
            _ => ""
        };
        AddRawEvent($"HatMove: Hat {value.Hat}, Value {positionName}");
    }

    private void HandleJoystickDisconnected(ref InputEvents.JoystickDisconnectedEvent value)
    {
        AddRawEvent($"JoystickDisconnected: Id {value.JoystickID}");
    }

    private void HandleTouchBegin(ref InputEvents.TouchBeginEvent value)
    {
        AddRawEvent($"TouchBegin: Id {value.TouchID}");
    }

    private void HandleTouchMove(ref InputEvents.TouchMoveEvent value)
    {
        AddRawEvent($"TouchMove: Id {value.TouchID}");
    }

    private void HandleTouchEnd(ref InputEvents.TouchEndEvent value)
    {
        AddRawEvent($"TouchEnd: Id {value.TouchID}");
    }

    /// <summary>Handle the logic update event.</summary>
    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || _sampleInput!.Pressed("Exit"))
            Exit();

        _direction = _aggregatedInput!.GetDirection();
        var directionText = _directionText.AsSpan();
        "Direction: "u8.CopyTo(directionText);
        var offset = "Direction: "u8.Length;
        _ = Utf8Formatter.TryFormat(_direction.X, directionText[offset..], out var written, new StandardFormat('F', 2));
        offset += written;
        directionText[offset++] = (byte)' ';
        _ = Utf8Formatter.TryFormat(_direction.Y, directionText[offset..], out written, new StandardFormat('F', 2));
        offset += written;
        directionText[offset++] = 0;
        _directionTextLength = offset;
    }

    private void Draw()
    {
        Vector2f viewportPosition;
        Vector2f viewportSize;
        uint viewportId;
        var viewport = ImGui.GetMainViewport();
        {
            viewportPosition = viewport.Pos;
            viewportSize = viewport.Size;
            viewportId = viewport.Id;
        }

        ImGui.SetNextWindowPos(viewportPosition);
        ImGui.SetNextWindowSize(viewportSize);
        ImGui.SetNextWindowViewport(viewportId);
        ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarWindowRounding, 0.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarWindowBorderSize, 0.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarWindowPadding, Vector2f.Zero);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColWindowBg, DashboardBackground);

        var windowOpen = true;
        if (ImGui.Begin("Aggregated Input"u8, ref windowOpen,
            ImGuiWindowFlags.ImGuiWindowFlagsNoDecoration |
            ImGuiWindowFlags.ImGuiWindowFlagsNoMove |
            ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings |
            ImGuiWindowFlags.ImGuiWindowFlagsNoBringToFrontOnFocus))
            DrawDashboard(viewportPosition, viewportSize);
        ImGui.End();
        ImGui.PopStyleColor();
        ImGui.PopStyleVar(3);
    }

    private void DrawDashboard(Vector2f viewportPosition, Vector2f viewportSize)
    {
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilledMultiColor(
            viewportPosition,
            viewportPosition + viewportSize,
            ImGui.GetColorU32(DashboardBackground),
            ImGui.GetColorU32(new Vector4f(0.055f, 0.075f, 0.13f, 1.0f)),
            ImGui.GetColorU32(new Vector4f(0.025f, 0.03f, 0.065f, 1.0f)),
            ImGui.GetColorU32(new Vector4f(0.035f, 0.025f, 0.075f, 1.0f)));

        var compact = viewportSize.X < 900.0f;
        var margin = compact ? 14.0f : 28.0f;
        var gap = compact ? 10.0f : 18.0f;
        var headerHeight = compact ? 40.0f : 48.0f;
        var contentWidth = Math.Max(1.0f, viewportSize.X - margin * 2.0f);
        var availableHeight = Math.Max(1.0f, viewportSize.Y - margin * 2.0f - headerHeight - gap);
        var stageHeight = Math.Max(250.0f, availableHeight * 0.58f);
        var logHeight = Math.Max(120.0f, availableHeight - stageHeight - gap);
        if (stageHeight + logHeight + gap > availableHeight)
            stageHeight = Math.Max(170.0f, availableHeight - logHeight - gap);

        var headerPosition = viewportPosition + new Vector2f(margin, margin);
        ImGui.SetCursorScreenPos(headerPosition);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColText, MutedText);
        ImGui.TextUnformatted("Input events update live."u8);
        ImGui.PopStyleColor();

        var stagePosition = viewportPosition + new Vector2f(margin, margin + headerHeight);
        var controlsWidth = compact ? Math.Max(210.0f, contentWidth * 0.36f) : Math.Min(360.0f, contentWidth * 0.31f);
        DrawControlsPanel(stagePosition, new Vector2f(controlsWidth, stageHeight));
        DrawDirectionPanel(
            stagePosition + new Vector2f(controlsWidth + gap, 0.0f),
            new Vector2f(contentWidth - controlsWidth - gap, stageHeight));

        var logsPosition = stagePosition + new Vector2f(0.0f, stageHeight + gap);
        var logWidth = (contentWidth - gap) * 0.5f;
        DrawEventPanel(logsPosition, new Vector2f(logWidth, logHeight), true);
        DrawEventPanel(logsPosition + new Vector2f(logWidth + gap, 0.0f),
            new Vector2f(logWidth, logHeight), false);
    }

    private void DrawControlsPanel(Vector2f position, Vector2f size)
    {
        BeginPanel("##Controls"u8, position, size);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColText, AccentViolet);
        ImGui.TextUnformatted("CONTROL SOURCES"u8);
        ImGui.PopStyleColor();
        DrawSourcePrompt(_sourcePrompts![0], "WASD / arrow keys"u8);

        var up = _dpadInput!.GetScancodeDown(Scancode.Up);
        var down = _dpadInput.GetScancodeDown(Scancode.Down);
        var left = _dpadInput.GetScancodeDown(Scancode.Left);
        var right = _dpadInput.GetScancodeDown(Scancode.Right);
        var keyboardOrigin = ImGui.GetCursorScreenPos() + new Vector2f(Math.Max(0.0f, (size.X - 112.0f) * 0.5f), 8.0f);
        DrawKey(_keyboardPrompts![0], keyboardOrigin + new Vector2f(36.0f, 0.0f), up);
        DrawKey(_keyboardPrompts[1], keyboardOrigin + new Vector2f(0.0f, 42.0f), left);
        DrawKey(_keyboardPrompts[2], keyboardOrigin + new Vector2f(36.0f, 42.0f), down);
        DrawKey(_keyboardPrompts[3], keyboardOrigin + new Vector2f(72.0f, 42.0f), right);
        ImGui.Dummy(new Vector2f(1.0f, 88.0f));

        ImGui.Separator();
        DrawSourcePrompt(_sourcePrompts[1], "Gamepad stick / directional pad"u8);
        DrawSourcePrompt(_sourcePrompts[2], "Drag touch to steer"u8);
        EndPanel();
    }

    private static void DrawSourcePrompt(Texture2D texture, ReadOnlySpan<byte> label)
    {
        ImGui.Image(texture, new Vector2f(24.0f, 24.0f));
        ImGui.SameLine();
        Widgets.AlignTextToLastItem();
        ImGui.PushStyleColor(ImGuiCol.ImGuiColText, MutedText);
        ImGui.TextUnformatted(label);
        ImGui.PopStyleColor();
    }

    private static void DrawKey(Texture2D texture, Vector2f position, bool active)
    {
        var drawList = ImGui.GetWindowDrawList();
        var keyMin = position - new Vector2f(3.0f, 3.0f);
        var keyMax = position + new Vector2f(35.0f, 35.0f);
        drawList.AddRectFilled(keyMin, keyMax,
            ImGui.GetColorU32(active ? AccentCyan : KeyBackground), 7.0f);
        if (active)
            drawList.AddRect(keyMin, keyMax, ImGui.GetColorU32(new Vector4f(0.6f, 1.0f, 1.0f, 1.0f)), 7.0f, 2.0f);
        ImGui.SetCursorScreenPos(position);
        ImGui.Image(texture, new Vector2f(32.0f, 32.0f));
    }

    private void DrawDirectionPanel(Vector2f position, Vector2f size)
    {
        BeginPanel("##Direction"u8, position, size);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColText, AccentCyan);
        ImGui.TextUnformatted("AGGREGATED OUTPUT"u8);
        ImGui.PopStyleColor();
        ImGui.PushStyleColor(ImGuiCol.ImGuiColText, MutedText);
        ImGui.TextUnformatted("All active inputs resolve into this vector"u8);
        ImGui.PopStyleColor();

        var contentStart = ImGui.GetCursorScreenPos();
        var arenaHeight = Math.Max(110.0f, size.Y - 118.0f);
        var radius = Math.Min(Math.Min(size.X * 0.24f, arenaHeight * 0.42f), 128.0f);
        var center = new Vector2f(position.X + size.X * 0.5f, contentStart.Y + arenaHeight * 0.5f);
        var drawList = ImGui.GetWindowDrawList();
        {
            drawList.AddCircleFilled(center, radius + 12.0f, ImGui.GetColorU32(new Vector4f(0.12f, 0.09f, 0.25f, 0.65f)));
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(new Vector4f(0.035f, 0.055f, 0.105f, 1.0f)));
            drawList.AddCircle(center, radius, ImGui.GetColorU32(new Vector4f(0.35f, 0.4f, 0.65f, 0.65f)), 0, 2.0f);
            drawList.AddCircle(center, radius * 0.5f, ImGui.GetColorU32(new Vector4f(0.25f, 0.3f, 0.5f, 0.5f)));
            drawList.AddLine(center - new Vector2f(radius, 0.0f), center + new Vector2f(radius, 0.0f),
                ImGui.GetColorU32(new Vector4f(0.22f, 0.28f, 0.5f, 0.55f)));
            drawList.AddLine(center - new Vector2f(0.0f, radius), center + new Vector2f(0.0f, radius),
                ImGui.GetColorU32(new Vector4f(0.22f, 0.28f, 0.5f, 0.55f)));
            var marker = center + new Vector2f(_direction.X, _direction.Y) * (radius * 0.72f);
            drawList.AddCircleFilled(marker, 18.0f, ImGui.GetColorU32(new Vector4f(0.1f, 0.95f, 1.0f, 0.18f)));
            drawList.AddCircleFilled(marker, 10.0f, ImGui.GetColorU32(AccentCyan));
            drawList.AddCircleFilled(marker, 3.0f, ImGui.GetColorU32(new Vector4f(0.9f, 1.0f, 1.0f, 1.0f)));
        }

        ImGui.SetCursorScreenPos(new Vector2f(position.X + 16.0f, position.Y + size.Y - 48.0f));
        if (_headingFont is not null)
            ImGui.PushFont(_headingFont, 28.0f);
        ImGui.TextUnformatted(_directionText.AsSpan(0, _directionTextLength));
        if (_headingFont is not null)
            ImGui.PopFont();
        EndPanel();
    }

    private void DrawEventPanel(Vector2f position, Vector2f size, bool raw)
    {
        BeginPanel(raw ? "##RawEvents"u8 : "##FilteredEvents"u8, position, size);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColText, raw ? AccentViolet : AccentCyan);
        ImGui.TextUnformatted(raw ? "RAW DEVICE EVENTS"u8 : "D-PAD ADAPTER EVENTS"u8);
        ImGui.PopStyleColor();
        ImGui.PushStyleColor(ImGuiCol.ImGuiColText, MutedText);
        ImGui.TextUnformatted(raw ? "Directly from keyboard, gamepad, and touch"u8 : "A clean directional event stream"u8);
        ImGui.PopStyleColor();
        ImGui.Separator();

        var events = raw ? _rawEvents : _filteredEvents;
        if (events.Count == 0)
        {
            ImGui.PushStyleColor(ImGuiCol.ImGuiColText, MutedText);
            ImGui.TextUnformatted("Waiting for input..."u8);
            ImGui.PopStyleColor();
        }
        else
        {
            for (var i = events.Count - 1; i >= 0; --i)
                ImGui.TextUnformatted(events[i]);
        }
        EndPanel();
    }

    private static void BeginPanel(ReadOnlySpan<byte> id, Vector2f position, Vector2f size)
    {
        ImGui.SetCursorScreenPos(position);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColChildBg, PanelBackground);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColBorder, PanelBorder);
        ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarChildRounding, 12.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarChildBorderSize, 1.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarWindowPadding, new Vector2f(16.0f, 14.0f));
        ImGui.BeginChild(id, size, ImGuiChildFlags.ImGuiChildFlagsBorders);
    }

    private static void EndPanel()
    {
        ImGui.EndChild();
        ImGui.PopStyleVar(3);
        ImGui.PopStyleColor(2);
    }

    protected override void Stop()
    {
        _sampleInput?.Dispose();
        _sampleInput = null;
        Deactivate();
        _dpadInput?.Dispose();
        _aggregatedInput?.Dispose();
    }

    private static readonly Vector4f DashboardBackground = new(0.02f, 0.025f, 0.055f, 1.0f);
    private static readonly Vector4f PanelBackground = new(0.055f, 0.065f, 0.115f, 0.94f);
    private static readonly Vector4f PanelBorder = new(0.2f, 0.24f, 0.42f, 0.8f);
    private static readonly Vector4f KeyBackground = new(0.13f, 0.15f, 0.24f, 1.0f);
    private static readonly Vector4f AccentCyan = new(0.18f, 0.92f, 1.0f, 1.0f);
    private static readonly Vector4f AccentViolet = new(0.68f, 0.47f, 1.0f, 1.0f);
    private static readonly Vector4f MutedText = new(0.56f, 0.62f, 0.74f, 1.0f);
    private SampleInputMap? _sampleInput;
}
