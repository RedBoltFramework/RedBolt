// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Sample.InputLogger;

using System.Buffers;
using System.Buffers.Text;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

using RedBolt;
using RedBolt.Bindings.DearImGui;
using RedBolt.DearImGui;
using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.Scene;

internal static class InputLoggerProgram
{
    private static int Main()
    {
        try
        {
            return new InputLoggerApplication().Run();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}

/// <summary>Input event logger example.</summary>
// TODO: port MainPluginApplication and URHO3D_DEFINE_PLUGIN_MAIN when the managed plugin application API is available.
// C++: class InputLogger : public MainPluginApplication
sealed class InputLoggerApplication : Application
{
    private sealed class LoggedEvent
    {
        public byte[] EventTypeText = [];
        public string EventType = string.Empty;
        public SortedDictionary<string, string> Parameters = new(StringComparer.Ordinal);
        public int Count;
        public uint TimeStamp;
    }

    private sealed class ViewportData
    {
        public Viewport Viewport = null!;
        public Scene Scene = null!;
        public Camera Camera = null!;
    }

    private readonly ViewportData?[] _viewports = new ViewportData?[2];
    private readonly List<byte[]> _displayLines = [];
    private ImFont? _headingFont;
    private uint _totalEventCount;
    private readonly List<LoggedEvent> _eventLog = [];

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.InputLogger";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.ResourcePaths = "CoreData;SampleData";
        parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowMaximize = false;
#if !REDBOLT_PLATFORM_BROWSER
        parameters.WindowWidth = 1280;
        parameters.WindowHeight = 720;
#endif
        parameters.WindowTitle = "RedBolt Input Logger";
    }

    private ViewportData CreateViewport(Color color, IntRect rect)
    {
        var scene = new Scene();
        scene.CreateComponent<Octree>();

        // Create zone
        var zoneNode = scene.CreateChild("Zone");
        var zone = zoneNode.CreateComponent<Zone>();
        zone.SetBoundingBox(new BoundingBox(-1000.0f, 1000.0f));
        zone.SetFogColor(color);

        // Create camera
        var cameraNode = scene.CreateChild("Camera");
        var camera = cameraNode.CreateComponent<Camera>();

        var viewport = new Viewport(scene, camera, rect);
        return new ViewportData { Viewport = viewport, Scene = scene, Camera = camera };
    }

    protected override void Start()
    {
        var dearImGui = new DearImGui((ImGuiConfigFlags)Engine.Parameters.DearImGuiFlags);
        Context.RegisterSubsystem(dearImGui);
        _headingFont = dearImGui.AddFont("Fonts/Anonymous Pro.ttf", size: 28.0f);
        SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>(
            (ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => Draw());

        // Create viewports
        _viewports[0] = CreateViewport(new Color(0x00 / 255f, 0x47 / 255f, 0xab / 255f), IntRect.Zero);
        _viewports[1] = CreateViewport(
            new Color(0x00 / 255f, 0x11 / 255f, 0x67 / 255f), new IntRect(350, 50, 450, 100));

        // Setup engine state
        Renderer.SetNumViewports(2);
        Renderer.SetViewport(0, _viewports[0]!.Viewport);
        Renderer.SetViewport(1, _viewports[1]!.Viewport);
        Input.SetMouseVisible(true);
        Input.SetMouseMode(MouseMode.Free);
        //Input.SetMouseMode(MouseMode.Wrap);

        SubscribeToEvent<InputEvents.MouseButtonDownEvent>(
            (ref InputEvents.MouseButtonDownEvent e) => OnInputEvent(DecodeEvent(
                "MouseButtonDown", ("Button", ((int)e.Button).ToString(CultureInfo.InvariantCulture)),
                ("Clicks", e.Clicks.ToString(CultureInfo.InvariantCulture)))));        SubscribeToEvent<InputEvents.MouseButtonUpEvent>(
            (ref InputEvents.MouseButtonUpEvent e) => OnInputEvent(DecodeEvent(
                "MouseButtonUp", ("Button", ((int)e.Button).ToString(CultureInfo.InvariantCulture)))));        SubscribeToEvent<InputEvents.MouseMoveEvent>(
            (ref InputEvents.MouseMoveEvent e) => OnInputEvent(DecodeEvent(
                "MouseMove", ("X", e.X.ToString(CultureInfo.InvariantCulture)),
                ("Y", e.Y.ToString(CultureInfo.InvariantCulture)))));        SubscribeToEvent<InputEvents.MouseWheelEvent>(
            (ref InputEvents.MouseWheelEvent e) => OnInputEvent(DecodeEvent(
                "MouseWheel", ("Wheel", e.Wheel.ToString(CultureInfo.InvariantCulture)))));        SubscribeToEvent<InputEvents.KeyDownEvent>(
            (ref InputEvents.KeyDownEvent e) => OnInputEvent(DecodeEvent(
                "KeyDown", ("Key", ((int)e.Key).ToString(CultureInfo.InvariantCulture)),
                ("Repeat", e.Repeat ? "true" : "false"))));        SubscribeToEvent<InputEvents.KeyUpEvent>(
            (ref InputEvents.KeyUpEvent e) => OnInputEvent(DecodeEvent(
                "KeyUp", ("Key", ((int)e.Key).ToString(CultureInfo.InvariantCulture)))));        SubscribeToEvent<InputEvents.TextInputEvent>(
            (ref InputEvents.TextInputEvent e) => OnInputEvent(DecodeEvent("TextInput", ("Text", e.Text))));        SubscribeToEvent<InputEvents.TextEditingEvent>(
            (ref InputEvents.TextEditingEvent e) =>
                OnInputEvent(DecodeEvent("TextEditing", ("Text", e.Composition))));        SubscribeToEvent<InputEvents.JoystickConnectedEvent>(
            (ref InputEvents.JoystickConnectedEvent e) => OnInputEvent(DecodeEvent(
                "JoystickConnected", ("JoystickID", e.JoystickID.ToString(CultureInfo.InvariantCulture)))));        SubscribeToEvent<InputEvents.JoystickDisconnectedEvent>(
            (ref InputEvents.JoystickDisconnectedEvent e) => OnInputEvent(DecodeEvent(
                "JoystickDisconnected", ("JoystickID", e.JoystickID.ToString(CultureInfo.InvariantCulture)))));        SubscribeToEvent<InputEvents.JoystickButtonDownEvent>(
            (ref InputEvents.JoystickButtonDownEvent e) => OnInputEvent(DecodeEvent(
                "JoystickButtonDown", ("JoystickID", e.JoystickID.ToString(CultureInfo.InvariantCulture)),
                ("Button", e.Button.ToString(CultureInfo.InvariantCulture)))));        SubscribeToEvent<InputEvents.JoystickButtonUpEvent>(
            (ref InputEvents.JoystickButtonUpEvent e) => OnInputEvent(DecodeEvent(
                "JoystickButtonUp", ("JoystickID", e.JoystickID.ToString(CultureInfo.InvariantCulture)),
                ("Button", e.Button.ToString(CultureInfo.InvariantCulture)))));        SubscribeToEvent<InputEvents.JoystickAxisMoveEvent>(
            (ref InputEvents.JoystickAxisMoveEvent e) => OnInputEvent(DecodeEvent(
                "JoystickAxisMove", ("JoystickID", e.JoystickID.ToString(CultureInfo.InvariantCulture)),
                ("Axis", e.Axis.ToString(CultureInfo.InvariantCulture)),
                ("Position", e.Position.ToString(CultureInfo.InvariantCulture)))));        SubscribeToEvent<InputEvents.JoystickHatMoveEvent>(
            (ref InputEvents.JoystickHatMoveEvent e) => OnInputEvent(DecodeEvent(
                "JoystickHatMove", ("JoystickID", e.JoystickID.ToString(CultureInfo.InvariantCulture)),
                ("Hat", e.Hat.ToString(CultureInfo.InvariantCulture)))));        SubscribeToEvent<InputEvents.TouchBeginEvent>(
            (ref InputEvents.TouchBeginEvent e) => OnInputEvent(DecodeEvent(
                "TouchBegin", ("TouchID", e.TouchID.ToString(CultureInfo.InvariantCulture)),
                ("Pressure", e.Pressure.ToString(CultureInfo.InvariantCulture)))));        SubscribeToEvent<InputEvents.TouchEndEvent>(
            (ref InputEvents.TouchEndEvent e) => OnInputEvent(DecodeEvent(
                "TouchEnd", ("TouchID", e.TouchID.ToString(CultureInfo.InvariantCulture)))));        SubscribeToEvent<InputEvents.TouchMoveEvent>(
            (ref InputEvents.TouchMoveEvent e) => OnInputEvent(DecodeEvent(
                "TouchMove", ("TouchID", e.TouchID.ToString(CultureInfo.InvariantCulture)),
                ("Pressure", e.Pressure.ToString(CultureInfo.InvariantCulture)))));        SubscribeToEvent<InputEvents.GestureRecordedEvent>(
            (ref InputEvents.GestureRecordedEvent e) => OnInputEvent(DecodeEvent(
                "GestureRecorded", ("GestureID", e.GestureID.ToString(CultureInfo.InvariantCulture)))));        SubscribeToEvent<InputEvents.GestureInputEvent>(
            (ref InputEvents.GestureInputEvent e) => OnInputEvent(DecodeEvent(
                "GestureInput", ("GestureID", e.GestureID.ToString(CultureInfo.InvariantCulture)),
                ("CenterX", e.CenterX.ToString(CultureInfo.InvariantCulture)),
                ("CenterY", e.CenterY.ToString(CultureInfo.InvariantCulture)),
                ("NumFingers", e.NumFingers.ToString(CultureInfo.InvariantCulture)),
                ("Error", e.Error.ToString(CultureInfo.InvariantCulture)))));        SubscribeToEvent<InputEvents.MultiGestureEvent>(
            (ref InputEvents.MultiGestureEvent e) => OnInputEvent(DecodeEvent(
                "MultiGesture", ("CenterX", e.CenterX.ToString(CultureInfo.InvariantCulture)),
                ("CenterY", e.CenterY.ToString(CultureInfo.InvariantCulture)),
                ("NumFingers", e.NumFingers.ToString(CultureInfo.InvariantCulture)),
                ("DTheta", e.DTheta.ToString(CultureInfo.InvariantCulture)),
                ("DDist", e.DDist.ToString(CultureInfo.InvariantCulture)))));

        UpdateText();
    }

    private void OnInputEvent(LoggedEvent inputEvent)
    {
        ++_totalEventCount;
        if (!MergeEvent(inputEvent))
            AddEvent(inputEvent);
        UpdateText();
    }

    private static LoggedEvent DecodeEvent(
        string eventType, params (string Name, string Value)[] parameters)
    {
        var inputEvent = new LoggedEvent
        {
            Count = 1,
            TimeStamp = Time.GetSystemTime(),
            EventTypeText = Encoding.UTF8.GetBytes(eventType),
            EventType = eventType,
        };
        foreach (var (name, value) in parameters)
            inputEvent.Parameters[name] = value;
        return inputEvent;
    }

    private bool MergeEvent(LoggedEvent inputEvent)
    {
        var sameEvent = _eventLog.Find(
            existingEvent => CanMergeEventWith(inputEvent, existingEvent));
        if (sameEvent is null)
            return false;

        ++sameEvent.Count;
        sameEvent.Parameters = inputEvent.Parameters;
        sameEvent.TimeStamp = inputEvent.TimeStamp;
        return true;
    }

    private static bool CanMergeEventWith(LoggedEvent inputEvent, LoggedEvent existingEvent)
    {
        var mergeable = inputEvent.EventType is
            "KeyDown" or
            "TextInput" or
            "MouseMove" or
            "MouseWheel" or
            "JoystickButtonDown" or
            "JoystickAxisMove" or
            "JoystickHatMove" or
            "TouchMove";

        const uint mergeThresholdMs = 250;
        if (inputEvent.EventType != existingEvent.EventType || !mergeable)
            return false;
        if (unchecked(inputEvent.TimeStamp - existingEvent.TimeStamp) > mergeThresholdMs)
            return false;

        foreach (var (name, oldValue) in existingEvent.Parameters)
        {
            if (name is "Clicks" or "X" or "Y" or "Position")
                continue;
            if (!inputEvent.Parameters.TryGetValue(name, out var newValue) || newValue != oldValue)
                return false;
        }

        return true;
    }

    private void AddEvent(LoggedEvent inputEvent)
    {
        _eventLog.Insert(0, inputEvent);
        while (_eventLog.Count > 100)
            _eventLog.RemoveAt(_eventLog.Count - 1);
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape))
            Exit();
    }

    private void UpdateText()
    {
        var mousePosition = Input.GetMousePosition();
        var backbufferSize = Input.GetBackbufferSize();

        _displayLines.Clear();
        AddLine(
            $"Mouse Position: {mousePosition.X} {mousePosition.Y} / {backbufferSize.X} {backbufferSize.Y}");

        for (var index = 0; index < 2; ++index)
        {
            var position = _viewports[index]!.Camera.MousePosition;
            var isInside = Rect.Positive.IsInside(position) != Intersection.Outside;
            AddLine($"- relative to Viewport #{index}: ({(isInside ? "in" : "out")}) {position}");
        }

        AddLine(string.Empty);

        foreach (var inputEvent in _eventLog)
        {
            var text = inputEvent.EventType;
            if (inputEvent.Count > 1)
                text += $" x{inputEvent.Count}";
            if (inputEvent.Parameters.Count != 0)
            {
                text += " (";
                var firstParam = true;
                foreach (var (name, value) in inputEvent.Parameters)
                {
                    if (!firstParam)
                        text += ", ";
                    text += name;
                    text += "=";
                    text += value;
                    firstParam = false;
                }
                text += ")";
            }
            AddLine(text);
        }
    }

    private void AddLine(string value)
    {
        _displayLines.Add(Encoding.UTF8.GetBytes(value));
    }

    private void Draw()
    {
        Vector2f viewportPosition;
        Vector2f viewportSize;
        uint viewportId;
        using (var viewport = ImGui.GetMainViewport())
        {
            viewportPosition = viewport.Pos;
            viewportSize = viewport.Size;
            viewportId = viewport.Id;
        ImGui.SetNextWindowPos(viewportPosition);
        ImGui.SetNextWindowSize(viewportSize);
        ImGui.SetNextWindowViewport(viewportId);
        ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarWindowRounding, 0.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarWindowBorderSize, 0.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarWindowPadding, Vector2f.Zero);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColWindowBg, DashboardBackground);
        var windowOpen = true;
        if (ImGui.Begin("Input Logger"u8, ref windowOpen,
            ImGuiWindowFlags.ImGuiWindowFlagsNoDecoration | ImGuiWindowFlags.ImGuiWindowFlagsNoMove |
            ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings |
            ImGuiWindowFlags.ImGuiWindowFlagsNoBringToFrontOnFocus))
            DrawDashboard(viewportPosition, viewportSize);
        }
        ImGui.PopStyleColor();
        ImGui.PopStyleVar(3);
        ImGui.End();
    }

    private void DrawDashboard(Vector2f position, Vector2f size)
    {
        using var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilledMultiColor(position, position + size,
            ImGui.GetColorU32(DashboardBackground), ImGui.GetColorU32(new Vector4f(0.05f, 0.075f, 0.14f, 1.0f)),
            ImGui.GetColorU32(new Vector4f(0.018f, 0.025f, 0.055f, 1.0f)),
            ImGui.GetColorU32(new Vector4f(0.055f, 0.025f, 0.095f, 1.0f)));
        var compact = size.X < 900.0f;
        var margin = compact ? 14.0f : 28.0f;
        var gap = compact ? 10.0f : 18.0f;
        var headerHeight = compact ? 54.0f : 64.0f;
        var metricsHeight = compact ? 68.0f : 76.0f;
        var width = Math.Max(1.0f, size.X - margin * 2.0f);
        var height = Math.Max(1.0f, size.Y - margin * 2.0f - headerHeight - metricsHeight - gap * 2.0f);
        var header = position + new Vector2f(margin, margin);
        DrawHeader(header, width, compact);

        var metrics = header + new Vector2f(0.0f, headerHeight);
        var metricWidth = (width - gap * 2.0f) / 3.0f;
        Span<byte> pointerText = stackalloc byte[48];
        var mouse = Input.GetMousePosition();
        var pointerLength = FormatPair(pointerText, mouse.X, mouse.Y, "  /  "u8);
        Span<byte> canvasText = stackalloc byte[48];
        var backbuffer = Input.GetBackbufferSize();
        var canvasLength = FormatPair(canvasText, backbuffer.X, backbuffer.Y, " x "u8);
        Span<byte> countText = stackalloc byte[24];
        _ = Utf8Formatter.TryFormat(_totalEventCount, countText, out var countLength);
        DrawMetric("##Pointer"u8, metrics, new Vector2f(metricWidth, metricsHeight),
            "POINTER"u8, pointerText[..pointerLength], AccentCyan);
        DrawMetric("##Canvas"u8, metrics + new Vector2f(metricWidth + gap, 0.0f),
            new Vector2f(metricWidth, metricsHeight), "CANVAS"u8, canvasText[..canvasLength], AccentViolet);
        DrawMetric("##Captured"u8, metrics + new Vector2f((metricWidth + gap) * 2.0f, 0.0f),
            new Vector2f(metricWidth, metricsHeight), "EVENTS CAPTURED"u8, countText[..countLength], AccentAmber);

        var main = metrics + new Vector2f(0.0f, metricsHeight + gap);
        var trackerWidth = compact ? width * 0.38f : Math.Min(420.0f, width * 0.36f);
        var eventPanelPosition = main + new Vector2f(trackerWidth + gap, 0.0f);
        var eventPanelSize = new Vector2f(width - trackerWidth - gap, height);
        UpdateEventViewportRect(eventPanelPosition, eventPanelSize, position, size);
        DrawViewportPanel(main, new Vector2f(trackerWidth, height));
        DrawEventPanel(eventPanelPosition, eventPanelSize);
    }

    private void UpdateEventViewportRect(
        Vector2f panelPosition, Vector2f panelSize, Vector2f windowPosition, Vector2f windowSize)
    {
        var backbufferSize = Input.GetBackbufferSize();
        var scale = new Vector2f(backbufferSize.X / windowSize.X, backbufferSize.Y / windowSize.Y);
        var localPosition = (panelPosition - windowPosition) * scale;
        var localSize = panelSize * scale;
        _viewports[1]!.Viewport.SetRect(new IntRect(
            (int)MathF.Round(localPosition.X),
            (int)MathF.Round(localPosition.Y),
            (int)MathF.Round(localPosition.X + localSize.X),
            (int)MathF.Round(localPosition.Y + localSize.Y)));
    }

    private void DrawHeader(Vector2f position, float width, bool compact)
    {
        ImGui.SetCursorScreenPos(position);
        if (_headingFont is not null)
            ImGui.PushFont(_headingFont, compact ? 22.0f : 28.0f);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColText, PrimaryText);
        ImGui.TextUnformatted("INPUT LOGGER"u8);
        ImGui.PopStyleColor();
        if (_headingFont is not null)
            ImGui.PopFont();
        ImGui.SetCursorScreenPos(position + new Vector2f(0.0f, compact ? 28.0f : 35.0f));
        ImGui.PushStyleColor(ImGuiCol.ImGuiColText, MutedText);
        ImGui.TextUnformatted("Every key, pointer, touch, and controller signal - decoded live."u8);
        ImGui.PopStyleColor();
        if (!compact)
        {
            var badge = position + new Vector2f(width - 166.0f, 5.0f);
            using var drawList = ImGui.GetWindowDrawList();
            drawList.AddRectFilled(badge, badge + new Vector2f(166.0f, 34.0f),
                ImGui.GetColorU32(new Vector4f(0.04f, 0.18f, 0.19f, 0.9f)), 17.0f);
            drawList.AddCircleFilled(badge + new Vector2f(17.0f, 17.0f), 4.0f, ImGui.GetColorU32(AccentCyan));
            ImGui.SetCursorScreenPos(badge + new Vector2f(30.0f, 8.0f));
            ImGui.PushStyleColor(ImGuiCol.ImGuiColText, AccentCyan);
            ImGui.TextUnformatted("CAPTURE IS LIVE"u8);
            ImGui.PopStyleColor();
        }
    }
    private void DrawMetric(ReadOnlySpan<byte> id, Vector2f position, Vector2f size,
        ReadOnlySpan<byte> label, ReadOnlySpan<byte> value, Vector4f accent)
    {
        BeginPanel(id, position, size);
        using (var drawList = ImGui.GetWindowDrawList())
            drawList.AddRectFilled(position, position + new Vector2f(4.0f, size.Y), ImGui.GetColorU32(accent), 10.0f);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColText, accent);
        ImGui.TextUnformatted(label);
        ImGui.PopStyleColor();
        if (_headingFont is not null)
            ImGui.PushFont(_headingFont, size.Y < 72.0f ? 19.0f : 23.0f);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColText, PrimaryText);
        ImGui.TextUnformatted(value);
        ImGui.PopStyleColor();
        if (_headingFont is not null)
            ImGui.PopFont();
        EndPanel();
    }

    private static int FormatPair(Span<byte> destination, int first, int second, ReadOnlySpan<byte> separator)
    {
        _ = Utf8Formatter.TryFormat(first, destination, out var written);
        separator.CopyTo(destination[written..]);
        written += separator.Length;
        _ = Utf8Formatter.TryFormat(second, destination[written..], out var secondWritten);
        return written + secondWritten;
    }

    private void DrawViewportPanel(Vector2f position, Vector2f size)
    {
        BeginPanel("##ViewportSignals"u8, position, size);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColText, AccentCyan);
        ImGui.TextUnformatted("VIEWPORT SIGNAL"u8);
        ImGui.PopStyleColor();
        ImGui.PushStyleColor(ImGuiCol.ImGuiColText, MutedText);
        ImGui.TextUnformatted("Hover the event stream to map Camera 02"u8);
        ImGui.PopStyleColor();
        var mapHeight = Math.Max(74.0f, (size.Y - 108.0f) * 0.5f);
        DrawViewportMap(0, mapHeight, AccentCyan);
        DrawViewportMap(1, mapHeight, AccentViolet);
        EndPanel();
    }

    private void DrawViewportMap(int index, float height, Vector4f accent)
    {
        var pointer = _viewports[index]!.Camera.MousePosition;
        var isInside = Rect.Positive.IsInside(pointer) != Intersection.Outside;
        var origin = ImGui.GetCursorScreenPos() + new Vector2f(0.0f, 7.0f);
        var width = Math.Max(80.0f, ImGui.GetContentRegionAvail().X);
        var mapMin = origin + new Vector2f(0.0f, 22.0f);
        var mapMax = mapMin + new Vector2f(width, Math.Max(36.0f, height - 34.0f));
        using (var drawList = ImGui.GetWindowDrawList())
        {
            drawList.AddRectFilled(mapMin, mapMax, ImGui.GetColorU32(MapBackground), 9.0f);
            drawList.AddRect(mapMin, mapMax, ImGui.GetColorU32(isInside ? accent : PanelBorder), 9.0f, 1.5f);
            drawList.AddLine(new Vector2f((mapMin.X + mapMax.X) * 0.5f, mapMin.Y),
                new Vector2f((mapMin.X + mapMax.X) * 0.5f, mapMax.Y), ImGui.GetColorU32(MapGrid));
            drawList.AddLine(new Vector2f(mapMin.X, (mapMin.Y + mapMax.Y) * 0.5f),
                new Vector2f(mapMax.X, (mapMin.Y + mapMax.Y) * 0.5f), ImGui.GetColorU32(MapGrid));
            if (isInside)
            {
                var marker = mapMin + pointer * (mapMax - mapMin);
                drawList.AddCircleFilled(marker, 11.0f, ImGui.GetColorU32(new Vector4f(
                    accent.X, accent.Y, accent.Z, 0.18f)));
                drawList.AddCircleFilled(marker, 5.0f, ImGui.GetColorU32(accent));
            }
        }
        ImGui.SetCursorScreenPos(origin);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColText, PrimaryText);
        ImGui.TextUnformatted(index == 0 ? "CAMERA 01  /  FULL WINDOW"u8 : "CAMERA 02  /  EVENT STREAM"u8);
        ImGui.PopStyleColor();
        Span<byte> pointerText = stackalloc byte[32];
        var pointerTextLength = isInside ? FormatPointer(pointerText, pointer) : 0;
        ImGui.SetCursorScreenPos(origin + new Vector2f(Math.Max(0.0f, width - (isInside ? 94.0f : 62.0f)), 0.0f));
        ImGui.PushStyleColor(ImGuiCol.ImGuiColText, isInside ? accent : MutedText);
        ImGui.TextUnformatted(isInside ? pointerText[..pointerTextLength] : "OUTSIDE"u8);
        ImGui.PopStyleColor();
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2f(width, height));
    }

    private static int FormatPointer(Span<byte> destination, Vector2f pointer)
    {
        destination[0] = (byte)'(';
        _ = Utf8Formatter.TryFormat(pointer.X, destination[1..], out var written, new StandardFormat('F', 2));
        var offset = written + 1;
        ", "u8.CopyTo(destination[offset..]);
        offset += 2;
        _ = Utf8Formatter.TryFormat(pointer.Y, destination[offset..], out written, new StandardFormat('F', 2));
        offset += written;
        destination[offset++] = (byte)')';
        return offset;
    }

    private void DrawEventPanel(Vector2f position, Vector2f size)
    {
        BeginPanel("##EventStream"u8, position, size);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColText, AccentViolet);
        ImGui.TextUnformatted("EVENT STREAM"u8);
        ImGui.PopStyleColor();
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.ImGuiColText, MutedText);
        ImGui.TextUnformatted("NEWEST FIRST  /  BURSTS ARE MERGED"u8);
        ImGui.PopStyleColor();
        ImGui.Separator();
        if (ImGui.BeginChild("##EventRows"u8, ImGui.GetContentRegionAvail()))
        {
            if (_eventLog.Count == 0)
                DrawEmptyState();
            else
            {
                for (var i = 0; i < _eventLog.Count; ++i)
                    DrawEventRow(_eventLog[i], _displayLines[i + 4]);
            }
        }
        ImGui.EndChild();
        EndPanel();
    }

    private static void DrawEmptyState()
    {
        var origin = ImGui.GetCursorScreenPos();
        var center = origin + ImGui.GetContentRegionAvail() * 0.5f;
        using (var drawList = ImGui.GetWindowDrawList())
        {
            drawList.AddCircleFilled(center - new Vector2f(0.0f, 22.0f), 28.0f,
                ImGui.GetColorU32(new Vector4f(0.14f, 0.11f, 0.27f, 0.75f)));
            drawList.AddCircle(center - new Vector2f(0.0f, 22.0f), 18.0f, ImGui.GetColorU32(AccentViolet), 0, 2.0f);
            drawList.AddCircleFilled(center - new Vector2f(0.0f, 22.0f), 5.0f, ImGui.GetColorU32(AccentCyan));
        }
        ImGui.SetCursorScreenPos(center + new Vector2f(-90.0f, 18.0f));
        ImGui.PushStyleColor(ImGuiCol.ImGuiColText, PrimaryText);
        ImGui.TextUnformatted("AWAITING YOUR SIGNAL"u8);
        ImGui.PopStyleColor();
        ImGui.SetCursorScreenPos(center + new Vector2f(-137.0f, 42.0f));
        ImGui.PushStyleColor(ImGuiCol.ImGuiColText, MutedText);
        ImGui.TextUnformatted("Move, click, type, touch, or connect a controller."u8);
        ImGui.PopStyleColor();
    }

    private static void DrawEventRow(LoggedEvent inputEvent, ReadOnlySpan<byte> text)
    {
        const float rowHeight = 54.0f;
        var origin = ImGui.GetCursorScreenPos();
        var width = Math.Max(120.0f, ImGui.GetContentRegionAvail().X);
        var accent = GetEventColor(inputEvent.EventType);
        using (var drawList = ImGui.GetWindowDrawList())
        {
            drawList.AddRectFilled(origin, origin + new Vector2f(width, rowHeight - 5.0f),
                ImGui.GetColorU32(EventBackground), 7.0f);
            drawList.AddRectFilled(origin, origin + new Vector2f(3.0f, rowHeight - 5.0f),
                ImGui.GetColorU32(accent), 7.0f);
        }
        ImGui.SetCursorScreenPos(origin + new Vector2f(14.0f, 7.0f));
        ImGui.PushStyleColor(ImGuiCol.ImGuiColText, accent);
        ImGui.TextUnformatted(inputEvent.EventTypeText);
        ImGui.PopStyleColor();
        ImGui.SetCursorScreenPos(origin + new Vector2f(14.0f, 27.0f));
        ImGui.PushStyleColor(ImGuiCol.ImGuiColText, MutedText);
        ImGui.TextUnformatted(text);
        ImGui.PopStyleColor();
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2f(width, rowHeight));
    }

    private static Vector4f GetEventColor(string eventType)
    {
        if (eventType.StartsWith("Mouse", StringComparison.Ordinal))
            return AccentCyan;
        if (eventType.StartsWith("Key", StringComparison.Ordinal) || eventType.StartsWith("Text", StringComparison.Ordinal))
            return AccentViolet;
        if (eventType.StartsWith("Touch", StringComparison.Ordinal) || eventType.Contains("Gesture", StringComparison.Ordinal))
            return AccentRose;
        if (eventType.StartsWith("Joystick", StringComparison.Ordinal))
            return AccentAmber;
        return AccentCyan;
    }

    private static void BeginPanel(ReadOnlySpan<byte> id, Vector2f position, Vector2f size)
    {
        ImGui.SetCursorScreenPos(position);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColChildBg, PanelBackground);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColBorder, PanelBorder);
        ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarChildRounding, 12.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarChildBorderSize, 1.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarWindowPadding, new Vector2f(16.0f, 12.0f));
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
        Renderer.SetNumViewports(0);

        Array.Clear(_viewports);
    }

    private static readonly Vector4f DashboardBackground = new(0.015f, 0.022f, 0.05f, 1.0f);
    private static readonly Vector4f PanelBackground = new(0.048f, 0.06f, 0.11f, 0.94f);
    private static readonly Vector4f PanelBorder = new(0.18f, 0.23f, 0.4f, 0.8f);
    private static readonly Vector4f MapBackground = new(0.022f, 0.035f, 0.075f, 1.0f);
    private static readonly Vector4f MapGrid = new(0.15f, 0.21f, 0.36f, 0.55f);
    private static readonly Vector4f EventBackground = new(0.068f, 0.078f, 0.135f, 0.84f);
    private static readonly Vector4f PrimaryText = new(0.91f, 0.95f, 1.0f, 1.0f);
    private static readonly Vector4f MutedText = new(0.51f, 0.58f, 0.71f, 1.0f);
    private static readonly Vector4f AccentCyan = new(0.16f, 0.93f, 1.0f, 1.0f);
    private static readonly Vector4f AccentViolet = new(0.68f, 0.46f, 1.0f, 1.0f);
    private static readonly Vector4f AccentAmber = new(1.0f, 0.68f, 0.26f, 1.0f);
    private static readonly Vector4f AccentRose = new(1.0f, 0.35f, 0.61f, 1.0f);
}
