// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Sample.SoundSynthesis;

using System;

using RedBolt;
using RedBolt.Audio;
using RedBolt.Bindings.DearImGui;
using RedBolt.DearImGui;
using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.Scene;
using Widgets = RedBolt.SystemUI.Widgets;

internal static class SoundSynthesisProgram
{
    private static int Main()
    {
        try
        {
            using var application = new SoundSynthesis();
            var exitCode = application.Run();
            if (exitCode == 0)
                Console.WriteLine("RedBolt SoundSynthesis sample finished.");
            return exitCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}

/// <summary>
/// Sound synthesis example.
/// This sample demonstrates:
///     - Playing back a sound stream produced on-the-fly by a simple CPU synthesis algorithm
/// </summary>
public class SoundSynthesis : Application
{
    private short[] _soundData = [];

    /// <summary>Construct.</summary>
    public SoundSynthesis()
    {
        _filter = 0.5f;
        _accumulator = 0.0f;
        _osc1 = 0.0f;
        _osc2 = 180.0f;
    }

    /// <summary>Setup after engine initialization and before running the main loop.</summary>
    protected override void Start()
    {
        _sampleInput = new SampleInputMap();
        _sampleInput.AddButton("IncreaseFilter", Scancode.Up, ControllerButton.RightShoulder, "+");
        _sampleInput.AddButton("DecreaseFilter", Scancode.Down, ControllerButton.LeftShoulder, "-", 1);
        Audio.RegisterAudioSubsystem(Context);

        // Execute base class startup
        base.Start();

        // Create the sound stream & start playback
        CreateSound();

        // Create the UI content
        CreateInstructions();

        // Hook up to the frame update events
        SubscribeToEvents();

        // Set the mouse mode to use in the sample
        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(true);
    }

    /// <summary>Construct the sound stream and start playback.</summary>
    private void CreateSound()
    {
        // Sound source needs a node so that it is considered enabled
        _node = new Node();
        var source = _node.CreateComponent<SoundSource>();
        source.SetIgnoreSceneTimeScale(true);

        _soundStream = new BufferedSoundStream();
        // Set format: 44100 Hz, sixteen bit, mono
        _soundStream.SetFormat(44100, true, false);

        // Start playback. We don't have data in the stream yet, but the SoundSource will wait until there is data,
        // as the stream is by default in the "don't stop at end" mode
        source.Play(_soundStream);
    }

    /// <summary>Buffer more sound data.</summary>
    private void UpdateSound()
    {
        // Try to keep 1/10 seconds of sound in the buffer, to avoid both dropouts and unnecessary latency
        var targetLength = 1.0f / 10.0f;
        var requiredLength = targetLength - _soundStream!.GetBufferLength();
        if (requiredLength < 0.0f)
            return;

        var numSamples = (int)(_soundStream.GetFrequency() * requiredLength);
        if (numSamples == 0)
            return;

        // Allocate a new buffer and fill it with a simple two-oscillator algorithm. The sound is over-amplified
        // (distorted), clamped to the 16-bit range, and finally lowpass-filtered according to the coefficient
        if (_soundData.Length < numSamples)
            Array.Resize(ref _soundData, numSamples);
        for (var i = 0; i < numSamples; ++i)
        {
            _osc1 = (_osc1 + 1.0f) % 360.0f;
            _osc2 = (_osc2 + 1.002f) % 360.0f;

            var newValue = MathDefs.Clamp((MathDefs.Sin(_osc1) + MathDefs.Sin(_osc2)) * 100000.0f, -32767.0f, 32767.0f);
            _accumulator = MathDefs.Lerp(_accumulator, newValue, _filter);
            _soundData[i] = (short)(int)_accumulator;
        }

        var waveformStep = Math.Max(1, numSamples / _waveform.Length);
        for (var i = 0; i < _waveform.Length; ++i)
            _waveform[i] = _soundData[Math.Min(i * waveformStep, numSamples - 1)] / 32768.0f;

        // Queue buffer to the stream for playback
        _soundStream.AddData(_soundData, checked((uint)(numSamples * sizeof(short))));
    }

    /// <summary>Construct an instruction text to the UI.</summary>
    private void CreateInstructions()
    {
        var dearImGui = new DearImGui((ImGuiConfigFlags)Engine.Parameters.DearImGuiFlags);
        Context.RegisterSubsystem(dearImGui);
        _headingFont = dearImGui.AddFont("Fonts/Anonymous Pro.ttf", size: 28.0f);
        SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>((ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => DrawInstructions());
        _controlPrompts =
        [
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_arrow_up.png"),
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_arrow_down.png")
        ];
    }

    /// <summary>Subscribe to application-wide logic update events.</summary>
    private void SubscribeToEvents()
    {
    }

    /// <summary>Handle the logic update event.</summary>
    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || _sampleInput!.Pressed("Exit"))
        {
            Exit();
            return;
        }

        // Use keys to control the filter constant
        if (_sampleInput.Evaluate("IncreaseFilter") != 0.0f)
            _filter += timeStep * 0.5f;
        if (_sampleInput.Evaluate("DecreaseFilter") != 0.0f)
            _filter -= timeStep * 0.5f;
        _filter = MathDefs.Clamp(_filter, 0.01f, 1.0f);

        UpdateSound();
    }

    private void DrawInstructions()
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
        ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarFrameRounding, 7.0f);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColWindowBg, DashboardBackground);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColFrameBg, ControlBackground);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColFrameBgHovered, new Vector4f(0.13f, 0.13f, 0.28f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.ImGuiColFrameBgActive, new Vector4f(0.19f, 0.16f, 0.38f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.ImGuiColSliderGrab, AccentViolet);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColSliderGrabActive, AccentCyan);
        var windowOpen = true;
        if (ImGui.Begin("Sound Synthesis"u8, ref windowOpen,
            ImGuiWindowFlags.ImGuiWindowFlagsNoDecoration |
            ImGuiWindowFlags.ImGuiWindowFlagsNoMove |
            ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings |
            ImGuiWindowFlags.ImGuiWindowFlagsNoBringToFrontOnFocus))
            DrawDashboard(viewportPosition, viewportSize);
        ImGui.End();
        ImGui.PopStyleColor(6);
        ImGui.PopStyleVar(4);
    }

    private void DrawDashboard(Vector2f position, Vector2f size)
    {
        var drawList = ImGui.GetWindowDrawList();
            drawList.AddRectFilledMultiColor(position, position + size,
                ImGui.GetColorU32(DashboardBackground), ImGui.GetColorU32(new Vector4f(0.075f, 0.035f, 0.15f, 1.0f)),
                ImGui.GetColorU32(new Vector4f(0.018f, 0.035f, 0.075f, 1.0f)),
                ImGui.GetColorU32(new Vector4f(0.02f, 0.105f, 0.12f, 1.0f)));

        var compact = size.X < 850.0f;
        var margin = compact ? 14.0f : 26.0f;
        var gap = compact ? 10.0f : 16.0f;
        var headerHeight = compact ? 58.0f : 68.0f;
        var width = Math.Max(1.0f, size.X - margin * 2.0f);
        var height = Math.Max(1.0f, size.Y - margin * 2.0f - headerHeight);
        var scopeWidth = (width - gap) * 0.67f;
        var controlsWidth = width - scopeWidth - gap;
        var controlsHeight = Math.Max(220.0f, height * 0.62f);
        var signalHeight = Math.Max(100.0f, height - controlsHeight - gap);
        if (controlsHeight + signalHeight + gap > height)
            controlsHeight = Math.Max(170.0f, height - signalHeight - gap);

        DrawHeader(position + new Vector2f(margin, margin), width, compact);
        var content = position + new Vector2f(margin, margin + headerHeight);
        DrawOscilloscope(content, new Vector2f(scopeWidth, height));
        DrawFilterPanel(content + new Vector2f(scopeWidth + gap, 0.0f), new Vector2f(controlsWidth, controlsHeight));
        DrawSignalPanel(content + new Vector2f(scopeWidth + gap, controlsHeight + gap),
            new Vector2f(controlsWidth, signalHeight));
    }

    private void DrawHeader(Vector2f position, float width, bool compact)
    {
        ImGui.SetCursorScreenPos(position);
        if (_headingFont is not null)
            ImGui.PushFont(_headingFont, compact ? 24.0f : 30.0f);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColText, PrimaryText);
        ImGui.TextUnformatted("SYNTHESIS OSCILLOSCOPE"u8);
        ImGui.PopStyleColor();
        if (_headingFont is not null)
            ImGui.PopFont();
        ImGui.SetCursorScreenPos(position + new Vector2f(0.0f, compact ? 29.0f : 35.0f));
        ImGui.PushStyleColor(ImGuiCol.ImGuiColText, MutedText);
        ImGui.TextUnformatted("A generated two-oscillator stream, visualized before it reaches the speakers."u8);
        ImGui.PopStyleColor();
        if (!compact)
        {
            var badge = position + new Vector2f(width - 152.0f, 4.0f);
            var drawList = ImGui.GetWindowDrawList();
            {
                drawList.AddRectFilled(badge, badge + new Vector2f(152.0f, 34.0f),
                    ImGui.GetColorU32(new Vector4f(0.035f, 0.18f, 0.18f, 0.9f)), 17.0f);
                drawList.AddCircleFilled(badge + new Vector2f(17.0f, 17.0f), 4.0f, ImGui.GetColorU32(AccentCyan));
            }
            ImGui.SetCursorScreenPos(badge + new Vector2f(30.0f, 8.0f));
            ImGui.PushStyleColor(ImGuiCol.ImGuiColText, AccentCyan);
            ImGui.TextUnformatted("STREAMING LIVE"u8);
            ImGui.PopStyleColor();
        }
    }

    private void DrawOscilloscope(Vector2f position, Vector2f size)
    {
        BeginPanel("##Oscilloscope"u8, position, size);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColText, AccentCyan);
        ImGui.TextUnformatted("LIVE WAVEFORM"u8);
        ImGui.PopStyleColor();
        ImGui.PushStyleColor(ImGuiCol.ImGuiColText, MutedText);
        ImGui.TextUnformatted("BUFFER OUTPUT  /  AMPLITUDE -1.0 TO +1.0"u8);
        ImGui.PopStyleColor();

        var scopeMin = ImGui.GetCursorScreenPos() + new Vector2f(0.0f, 12.0f);
        var scopeMax = position + size - new Vector2f(16.0f, 18.0f);
        var scopeSize = scopeMax - scopeMin;
        var drawList = ImGui.GetWindowDrawList();
        {
            drawList.AddRectFilled(scopeMin, scopeMax, ImGui.GetColorU32(ScopeBackground), 9.0f);
            for (var i = 1; i < 8; ++i)
            {
                var x = scopeMin.X + scopeSize.X * i / 8.0f;
                drawList.AddLine(new Vector2f(x, scopeMin.Y), new Vector2f(x, scopeMax.Y), ImGui.GetColorU32(ScopeGrid));
            }
            for (var i = 1; i < 6; ++i)
            {
                var y = scopeMin.Y + scopeSize.Y * i / 6.0f;
                drawList.AddLine(new Vector2f(scopeMin.X, y), new Vector2f(scopeMax.X, y),
                    ImGui.GetColorU32(i == 3 ? ScopeCenter : ScopeGrid), 1.0f);
            }
            for (var i = 1; i < _waveform.Length; ++i)
            {
                var a = new Vector2f(
                    scopeMin.X + scopeSize.X * (i - 1) / (_waveform.Length - 1),
                    scopeMin.Y + scopeSize.Y * (0.5f - _waveform[i - 1] * 0.42f));
                var b = new Vector2f(
                    scopeMin.X + scopeSize.X * i / (_waveform.Length - 1),
                    scopeMin.Y + scopeSize.Y * (0.5f - _waveform[i] * 0.42f));
                drawList.AddLine(a, b, ImGui.GetColorU32(new Vector4f(0.08f, 0.85f, 1.0f, 0.14f)), 7.0f);
                drawList.AddLine(a, b, ImGui.GetColorU32(AccentCyan), 2.0f);
            }
            drawList.AddRect(scopeMin, scopeMax, ImGui.GetColorU32(PanelBorder), 9.0f, 1.5f);
        }
        ImGui.SetCursorScreenPos(scopeMin);
        ImGui.Dummy(scopeSize);
        EndPanel();
    }

    private void DrawFilterPanel(Vector2f position, Vector2f size)
    {
        BeginPanel("##Filter"u8, position, size);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColText, AccentViolet);
        ImGui.TextUnformatted("LOW-PASS FILTER"u8);
        ImGui.PopStyleColor();
        ImGui.PushStyleColor(ImGuiCol.ImGuiColText, MutedText);
        ImGui.TextUnformatted("Shape the generated harmonics."u8);
        ImGui.PopStyleColor();
        ImGui.Separator();

        ImGui.Image(SampleInputMap.GetActionPrompt(_controlPrompts![0], ControllerButton.RightShoulder),
            PromptIconSize);
        ImGui.SameLine();
        ImGui.Image(SampleInputMap.GetActionPrompt(_controlPrompts[1], ControllerButton.LeftShoulder),
            PromptIconSize);
        ImGui.SameLine();
        Widgets.AlignTextToLastItem();
        ImGui.PushStyleColor(ImGuiCol.ImGuiColText, PrimaryText);
        ImGui.TextUnformatted(Input.ActiveInputDevice == InputDeviceType.KeyboardMouse
            ? "Arrow keys"u8
            : "Increase / decrease cutoff"u8);
        ImGui.PopStyleColor();
        ImGui.Dummy(new Vector2f(1.0f, 8.0f));
        ImGui.SetNextItemWidth(Math.Max(90.0f, size.X - 32.0f));
        ImGui.SliderFloat("##FilterCoefficient"u8, ref _filter, 0.01f, 1.0f);

        ImGui.PushStyleColor(ImGuiCol.ImGuiColText, AccentCyan);
        ImGui.TextUnformatted(_filter < 0.18f ? "SOFT / HEAVILY FILTERED"u8 :
            _filter < 0.65f ? "BALANCED RESPONSE"u8 : "BRIGHT / NEAR RAW"u8);
        ImGui.PopStyleColor();
        ImGui.PushStyleColor(ImGuiCol.ImGuiColText, MutedText);
        ImGui.TextUnformatted("Low is soft. High stays bright."u8);
        ImGui.PopStyleColor();
        EndPanel();
    }

    private static void DrawSignalPanel(Vector2f position, Vector2f size)
    {
        BeginPanel("##Signal"u8, position, size);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColText, AccentAmber);
        ImGui.TextUnformatted("SIGNAL FORMAT"u8);
        ImGui.PopStyleColor();
        ImGui.PushStyleColor(ImGuiCol.ImGuiColText, PrimaryText);
        ImGui.TextUnformatted("44.1 KHZ  /  16-BIT  /  MONO"u8);
        ImGui.PopStyleColor();
        ImGui.PushStyleColor(ImGuiCol.ImGuiColText, MutedText);
        ImGui.TextUnformatted("100 ms target / low-latency playback."u8);
        ImGui.PopStyleColor();
        EndPanel();
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

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.SoundSynthesis";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.WindowMaximize = false;
        parameters.ResourcePaths = "CoreData;SampleData";
        parameters.ResourcePrefixPaths = ".;..";
#if !REDBOLT_PLATFORM_BROWSER
            parameters.WindowHeight = 540;
            parameters.WindowWidth = 960;
#endif
        parameters.WindowTitle = "RedBolt Sound Synthesis";
    }

    protected override void Stop()
    {
        _sampleInput?.Dispose();
        _sampleInput = null;
        _soundStream = null;
        _node?.Dispose();
        _node = null;
        base.Stop();
    }

    public override void Dispose()
    {
        _soundStream = null;
        _node?.Dispose();
        _node = null;
        base.Dispose();
    }


    /// <summary>Scene node for the sound component.</summary>
    private Node? _node;
    /// <summary>Sound stream that we update.</summary>
    private BufferedSoundStream? _soundStream;
    /// <summary>Filter coefficient for the sound.</summary>
    private float _filter;
    /// <summary>Synthesis accumulator.</summary>
    private float _accumulator;
    /// <summary>First oscillator.</summary>
    private float _osc1;
    /// <summary>Second oscillator.</summary>
    private float _osc2;
    private Texture2D[]? _controlPrompts;
    private ImFont? _headingFont;
    private readonly float[] _waveform = new float[256];
    private static readonly Vector2f PromptIconSize = new(28.0f, 28.0f);
    private static readonly Vector4f DashboardBackground = new(0.015f, 0.02f, 0.055f, 1.0f);
    private static readonly Vector4f PanelBackground = new(0.05f, 0.055f, 0.115f, 0.95f);
    private static readonly Vector4f PanelBorder = new(0.2f, 0.2f, 0.42f, 0.85f);
    private static readonly Vector4f ControlBackground = new(0.095f, 0.09f, 0.2f, 1.0f);
    private static readonly Vector4f ScopeBackground = new(0.012f, 0.032f, 0.06f, 1.0f);
    private static readonly Vector4f ScopeGrid = new(0.11f, 0.24f, 0.31f, 0.5f);
    private static readonly Vector4f ScopeCenter = new(0.2f, 0.52f, 0.58f, 0.75f);
    private static readonly Vector4f PrimaryText = new(0.93f, 0.96f, 1.0f, 1.0f);
    private static readonly Vector4f MutedText = new(0.54f, 0.59f, 0.73f, 1.0f);
    private static readonly Vector4f AccentCyan = new(0.12f, 0.94f, 1.0f, 1.0f);
    private static readonly Vector4f AccentViolet = new(0.72f, 0.46f, 1.0f, 1.0f);
    private static readonly Vector4f AccentAmber = new(1.0f, 0.68f, 0.25f, 1.0f);
    private SampleInputMap? _sampleInput;
}
