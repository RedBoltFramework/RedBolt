// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Sample.SoundEffects;

using System;
using System.Collections.Generic;

using RedBolt;
using RedBolt.Audio;
using RedBolt.Bindings.DearImGui;
using RedBolt.DearImGui;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.Input;
using RedBolt.Scene;

internal static class SoundEffectsProgram
{
    private static int Main()
    {
        try
        {
            using var application = new SoundEffects();
            var exitCode = application.Run();
            if (exitCode == 0)
                Console.WriteLine("RedBolt SoundEffects sample finished.");
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
/// Sound effects example.
/// This sample demonstrates:
///     - Playing sound effects and music
///     - Controlling sound and music master volume
/// </summary>
public class SoundEffects : Application
{
    /// <summary>Construct.</summary>
    public SoundEffects()
    {
    }

    /// <summary>Setup after engine initialization and before running the main loop.</summary>
    protected override void Start()
    {
        _sampleInput = new SampleInputMap();
        Audio.RegisterAudioSubsystem(Context);

        // Execute base class startup
        base.Start();

        // Create a scene which will not be actually rendered, but is used to hold SoundSource components while they play sounds
        _scene = new Scene();

        // Create music sound source
        _musicSource = _scene.CreateComponent<SoundSource>();
        // Set the sound type to music so that master volume control works correctly
        _musicSource.SetSoundType(AudioDefs.SoundMusic);

        // Enable OS cursor
        Input.SetMouseVisible(true);

        // Create the user interface
        CreateUI();

        // Set the mouse mode to use in the sample
        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(true);
    }

    protected SoundSource? _musicSource;

    /// <summary>Create the UI and subscribes to UI events.</summary>
    private void CreateUI()
    {
        var dearImGui = new DearImGui((ImGuiConfigFlags)Engine.Parameters.DearImGuiFlags);
        Context.RegisterSubsystem(dearImGui);
        _headingFont = dearImGui.AddFont("Fonts/Anonymous Pro.ttf", size: 28.0f);
        SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>((ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => DrawUI());

        var audio = GetSubsystem<Audio>()!;
        _microphones = audio.EnumerateMicrophones();
        for (var i = 0; i < _microphones.Count; ++i)
            _microphoneLabels.Add(new CString(_microphones[i]));
    }

    /// <summary>Handle a sound effect button click.</summary>
    private void HandlePlaySound(string soundResourceName)
    {
        // Get the sound resource
        var sound = Resources.Get<Sound>(soundResourceName);

        if (sound is not null)
        {
            // Create a SoundSource component for playing the sound. The SoundSource component plays
            // non-positional audio, so its 3D position in the scene does not matter. For positional sounds the
            // SoundSource3D component would be used instead
            var soundSource = _scene!.CreateComponent<SoundSource>();
            // Component will automatically remove itself when the sound finished playing
            soundSource.SetAutoRemoveMode(AutoRemoveMode.Component);
            soundSource.Play(sound);
            // In case we also play music, set the sound volume below maximum so that we don't clip the output
            soundSource.SetGain(0.75f);
            soundSource.SetPanning(_pan);
            soundSource.SetReach(_reach);
            soundSource.SetLowFrequency(_lfe);
        }
    }

    /// <summary>Handle "play music" button click.</summary>
    private void HandlePlayMusic()
    {
        var music = Resources.Get<Sound>("Music/Ninja Gods.ogg");
        // Set the song to loop
        music.SetLooped(true);

        _musicSource!.Play(music);
    }

    /// <summary>Handle "stop music" button click.</summary>
    private void HandleStopMusic()
    {
        // Remove the music player node from the scene
        _musicSource!.Stop();
    }

    /// <summary>Handle "pause scene" button click.</summary>
    private void HandlePauseScene()
    {
        _scene!.SetUpdateEnabled(false);
    }

    /// <summary>Handle "resume scene" button click.</summary>
    private void HandleResumeScene()
    {
        _scene!.SetUpdateEnabled(true);
    }

    /// <summary>Handle scene time scale slider change.</summary>
    private void HandleSceneTimeScale(float newScale)
    {
        _scene!.SetTimeScale(newScale);
    }

    /// <summary>Handle sound effects volume slider change.</summary>
    private void HandleSoundVolume(float newVolume)
    {
        GetSubsystem<Audio>()!.SetMasterGain(AudioDefs.SoundEffect, newVolume);
    }

    /// <summary>Handle sound effect L/R pan slider change.</summary>
    private void HandleSoundPan(float value)
    {
        _pan = value * 2.0f - 1.0f;
    }

    /// <summary>Handle sound effect F/B reach slider change.</summary>
    private void HandleSoundReach(float value)
    {
        _reach = value * 2.0f - 1.0f;
    }

    /// <summary>Handle sound effect F/B reach slider change.</summary>
    private void HandleLFE(bool state)
    {
        _lfe = state;
    }

    /// <summary>Handle music volume slider change.</summary>
    private void HandleMusicVolume(float newVolume)
    {
        GetSubsystem<Audio>()!.SetMasterGain(AudioDefs.SoundMusic, newVolume);
    }

    /// <summary>Handle starting mic recording.</summary>
    private void HandleStartMicRecord()
    {
        if (_activeMic is not null)
            return;

        if (_microphones.Count != 0)
        {
            var micName = _microphones[_selectedMicrophone];

            _activeMic = GetSubsystem<Audio>()!.CreateMicrophone(micName, false, 16000, 64);
            if (_activeMic is not null)
            {
                _micStream = new BufferedSoundStream();
                _micStream.SetFormat(_activeMic.GetFrequency(), true, false);
                _activeMic.Link(_micStream);
            }
        }
        else
            Console.Error.WriteLine("No microphones detected");
    }

    /// <summary>Handle stopping mic recording.</summary>
    private void HandleStopMicRecord()
    {
        _activeMic?.Dispose();
        _activeMic = null;

        if (_micStream is not null)
        {
            var soundSource = _scene!.CreateComponent<SoundSource>();
            // Component will automatically remove itself when the sound finished playing
            soundSource.SetAutoRemoveMode(AutoRemoveMode.Component);

            using var snd = new Sound();
            soundSource.Play(_micStream);
            soundSource.SetGain(1.0f);
            soundSource.SetPanning(0.0f);
            soundSource.SetReach(0.0f);
        }
    }

    private void DrawUI()
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
        ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarFrameRounding, 6.0f);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColWindowBg, DashboardBackground);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColFrameBg, ControlBackground);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColFrameBgHovered, ControlHovered);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColFrameBgActive, ControlActive);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColSliderGrab, AccentCyan);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColSliderGrabActive, new Vector4f(0.55f, 1.0f, 1.0f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.ImGuiColButton, ControlBackground);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColButtonHovered, ControlHovered);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColButtonActive, ControlActive);
        var windowOpen = true;
        if (ImGui.Begin("Sound Effects"u8, ref windowOpen,
            ImGuiWindowFlags.ImGuiWindowFlagsNoDecoration |
            ImGuiWindowFlags.ImGuiWindowFlagsNoMove |
            ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings |
            ImGuiWindowFlags.ImGuiWindowFlagsNoBringToFrontOnFocus))
            DrawDashboard(viewportPosition, viewportSize);
        ImGui.End();
        ImGui.PopStyleColor(9);
        ImGui.PopStyleVar(4);
    }

    private void DrawDashboard(Vector2f position, Vector2f size)
    {
        var drawList = ImGui.GetWindowDrawList();
            drawList.AddRectFilledMultiColor(position, position + size,
                ImGui.GetColorU32(DashboardBackground), ImGui.GetColorU32(new Vector4f(0.035f, 0.11f, 0.15f, 1.0f)),
                ImGui.GetColorU32(new Vector4f(0.02f, 0.035f, 0.075f, 1.0f)),
                ImGui.GetColorU32(new Vector4f(0.07f, 0.025f, 0.1f, 1.0f)));

        var compact = size.X < 850.0f;
        var margin = compact ? 14.0f : 26.0f;
        var gap = compact ? 10.0f : 16.0f;
        var headerHeight = compact ? 58.0f : 68.0f;
        var width = Math.Max(1.0f, size.X - margin * 2.0f);
        var height = Math.Max(1.0f, size.Y - margin * 2.0f - headerHeight);
        var topHeight = Math.Max(180.0f, height * 0.61f);
        var bottomHeight = Math.Max(110.0f, height - topHeight - gap);
        if (topHeight + bottomHeight + gap > height)
            topHeight = Math.Max(160.0f, height - bottomHeight - gap);
        var leftWidth = (width - gap) * 0.58f;
        var rightWidth = width - leftWidth - gap;

        DrawHeader(position + new Vector2f(margin, margin), width, compact);
        var content = position + new Vector2f(margin, margin + headerHeight);
        DrawEffectsPanel(content, new Vector2f(leftWidth, topHeight));
        DrawPlaybackPanel(content + new Vector2f(leftWidth + gap, 0.0f), new Vector2f(rightWidth, topHeight));
        DrawMixerPanel(content + new Vector2f(0.0f, topHeight + gap), new Vector2f(leftWidth, bottomHeight));
        DrawMicrophonePanel(content + new Vector2f(leftWidth + gap, topHeight + gap),
            new Vector2f(rightWidth, bottomHeight));
    }

    private void DrawHeader(Vector2f position, float width, bool compact)
    {
        ImGui.SetCursorScreenPos(position);
        if (_headingFont is not null)
            ImGui.PushFont(_headingFont, compact ? 24.0f : 30.0f);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColText, PrimaryText);
        ImGui.TextUnformatted("SOUND LAB"u8);
        ImGui.PopStyleColor();
        if (_headingFont is not null)
            ImGui.PopFont();
        ImGui.SetCursorScreenPos(position + new Vector2f(0.0f, compact ? 29.0f : 35.0f));
        ImGui.PushStyleColor(ImGuiCol.ImGuiColText, MutedText);
        ImGui.TextUnformatted("Playback, routing, recording, and timing in one live mix."u8);
        ImGui.PopStyleColor();
        if (!compact)
        {
            var badge = position + new Vector2f(width - 174.0f, 4.0f);
            var drawList = ImGui.GetWindowDrawList();
            {
                drawList.AddRectFilled(badge, badge + new Vector2f(174.0f, 34.0f),
                    ImGui.GetColorU32(new Vector4f(0.035f, 0.17f, 0.19f, 0.92f)), 17.0f);
                drawList.AddCircleFilled(badge + new Vector2f(17.0f, 17.0f), 4.0f, ImGui.GetColorU32(AccentCyan));
            }
            ImGui.SetCursorScreenPos(badge + new Vector2f(30.0f, 8.0f));
            ImGui.PushStyleColor(ImGuiCol.ImGuiColText, AccentCyan);
            ImGui.TextUnformatted("AUDIO ENGINE ONLINE"u8);
            ImGui.PopStyleColor();
        }
    }

    private void DrawEffectsPanel(Vector2f position, Vector2f size)
    {
        BeginPanel("##Effects"u8, position, size);
        DrawPanelTitle("ONE-SHOT EFFECTS"u8, "Trigger a sound, then shape its spatial output."u8, AccentCyan);

        if (ImGui.Button("FIST HIT"u8))
            HandlePlaySound("Sounds/PlayerFistHit.wav");
        ImGui.SameLine();
        if (ImGui.Button("EXPLOSION"u8))
            HandlePlaySound("Sounds/BigExplosion.wav");
        ImGui.SameLine();
        if (ImGui.Button("POWER-UP"u8))
            HandlePlaySound("Sounds/Powerup.wav");

        ImGui.Dummy(new Vector2f(1.0f, 7.0f));
        var pan = (_pan + 1.0f) * 0.5f;
        ImGui.SetNextItemWidth(Math.Max(90.0f, size.X - 150.0f));
        if (ImGui.SliderFloat("L  PAN  R"u8, ref pan, 0.0f, 1.0f))
            HandleSoundPan(pan);

        var reach = (_reach + 1.0f) * 0.5f;
        ImGui.SetNextItemWidth(Math.Max(90.0f, size.X - 150.0f));
        if (ImGui.SliderFloat("BACK  REACH  FRONT"u8, ref reach, 0.0f, 1.0f))
            HandleSoundReach(reach);

        var lfe = _lfe;
        if (ImGui.Checkbox("Route low frequencies to LFE"u8, ref lfe))
            HandleLFE(lfe);
        EndPanel();
    }

    private void DrawPlaybackPanel(Vector2f position, Vector2f size)
    {
        BeginPanel("##Playback"u8, position, size);
        DrawPanelTitle("PLAYBACK STATE"u8, "Music and scene time remain independent."u8, AccentViolet);
        DrawStateLine("MUSIC"u8, _musicSource!.IsPlaying(), "PLAYING"u8, "STOPPED"u8);
        if (ImGui.Button("PLAY"u8))
            HandlePlayMusic();
        ImGui.SameLine();
        ImGui.BeginDisabled(!_musicSource.IsPlaying());
        if (ImGui.Button("STOP"u8))
            HandleStopMusic();
        ImGui.EndDisabled();

        ImGui.Dummy(new Vector2f(1.0f, 6.0f));
        var sceneRunning = _scene!.IsUpdateEnabled();
        DrawStateLine("SCENE CLOCK"u8, sceneRunning, "RUNNING"u8, "PAUSED"u8);
        ImGui.BeginDisabled(!sceneRunning);
        if (ImGui.Button("PAUSE"u8))
            HandlePauseScene();
        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.BeginDisabled(sceneRunning);
        if (ImGui.Button("RESUME"u8))
            HandleResumeScene();
        ImGui.EndDisabled();

        var sceneTimeScale = _scene.GetTimeScale();
        ImGui.SetNextItemWidth(Math.Max(80.0f, size.X - 145.0f));
        if (ImGui.SliderFloat("TIME SCALE"u8, ref sceneTimeScale, 0.0f, 2.0f))
            HandleSceneTimeScale(sceneTimeScale);
        EndPanel();
    }

    private void DrawMixerPanel(Vector2f position, Vector2f size)
    {
        BeginPanel("##Mixer"u8, position, size);
        DrawPanelTitle("MASTER MIX"u8, "Balance effects and music buses."u8, AccentAmber);
        var audio = GetSubsystem<Audio>()!;
        var soundVolume = audio.GetMasterGain(AudioDefs.SoundEffect);
        ImGui.SetNextItemWidth(Math.Max(100.0f, size.X - 175.0f));
        if (ImGui.SliderFloat("EFFECTS"u8, ref soundVolume, 0.0f, 1.0f))
            HandleSoundVolume(soundVolume);
        var musicVolume = audio.GetMasterGain(AudioDefs.SoundMusic);
        ImGui.SetNextItemWidth(Math.Max(100.0f, size.X - 175.0f));
        if (ImGui.SliderFloat("MUSIC"u8, ref musicVolume, 0.0f, 1.0f))
            HandleMusicVolume(musicVolume);
        EndPanel();
    }

    private void DrawMicrophonePanel(Vector2f position, Vector2f size)
    {
        BeginPanel("##Microphone"u8, position, size);
        DrawPanelTitle("MICROPHONE LOOPBACK"u8, "Capture a clip and replay it on stop."u8, AccentRose);
        if (_microphoneLabels.Count == 0)
            DrawStateLine("INPUT"u8, false, "READY"u8, "NO DEVICE"u8, true);
        else
        {
            ImGui.SetNextItemWidth(Math.Max(90.0f, size.X - 80.0f));
            if (ImGui.BeginCombo("##Input"u8, _microphoneLabels[_selectedMicrophone].ContentBytes))
            {
                for (var i = 0; i < _microphoneLabels.Count; ++i)
                {
                    if (ImGui.Selectable(_microphoneLabels[i].ContentBytes, i == _selectedMicrophone))
                        _selectedMicrophone = i;
                }
                ImGui.EndCombo();
            }
        }

        ImGui.BeginDisabled(_activeMic is not null || _microphoneLabels.Count == 0);
        if (ImGui.Button("RECORD"u8))
            HandleStartMicRecord();
        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.BeginDisabled(_activeMic is null);
        if (ImGui.Button("STOP + PLAY"u8))
            HandleStopMicRecord();
        ImGui.EndDisabled();
        ImGui.SameLine();
        RenderState(_activeMic is not null, "RECORDING"u8, "IDLE"u8);
        EndPanel();
    }

    private static void RenderState(bool active, ReadOnlySpan<byte> activeText, ReadOnlySpan<byte> inactiveText)
    {
        ImGui.PushStyleColor(
            ImGuiCol.ImGuiColText,
            active ? AccentCyan : MutedText);
        ImGui.TextUnformatted(active ? activeText : inactiveText);
        ImGui.PopStyleColor();
    }

    private static void DrawPanelTitle(ReadOnlySpan<byte> title, ReadOnlySpan<byte> subtitle, Vector4f accent)
    {
        ImGui.PushStyleColor(ImGuiCol.ImGuiColText, accent);
        ImGui.TextUnformatted(title);
        ImGui.PopStyleColor();
        ImGui.PushStyleColor(ImGuiCol.ImGuiColText, MutedText);
        ImGui.TextUnformatted(subtitle);
        ImGui.PopStyleColor();
        ImGui.Separator();
    }

    private static void DrawStateLine(ReadOnlySpan<byte> label, bool active, ReadOnlySpan<byte> activeText,
        ReadOnlySpan<byte> inactiveText, bool failure = false)
    {
        ImGui.PushStyleColor(ImGuiCol.ImGuiColText, MutedText);
        ImGui.TextUnformatted(label);
        ImGui.PopStyleColor();
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.ImGuiColText, active ? AccentCyan : failure ? AccentRose : MutedText);
        ImGui.TextUnformatted(active ? activeText : inactiveText);
        ImGui.PopStyleColor();
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
        parameters.ApplicationName = "RedBolt.Sample.SoundEffects";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.WindowMaximize = false;
        parameters.ResourcePaths = "CoreData;SampleData";
        parameters.ResourcePrefixPaths = ".;..";
#if !REDBOLT_PLATFORM_BROWSER
            parameters.WindowHeight = 540;
            parameters.WindowWidth = 960;
#endif
        parameters.WindowTitle = "RedBolt Sound Effects";
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || _sampleInput!.Pressed("Exit"))
            Exit();
    }

    protected override void Stop()
    {
        _sampleInput?.Dispose();
        _sampleInput = null;
        _activeMic?.Dispose();
        _activeMic = null;
        _micStream = null;
        _musicSource = null;
        _scene?.Dispose();
        _scene = null;
        for (var i = _microphoneLabels.Count - 1; i >= 0; --i)
            _microphoneLabels[i].Dispose();
        _microphoneLabels.Clear();
        _microphones = [];
        base.Stop();
    }

    public override void Dispose()
    {
        _activeMic?.Dispose();
        _activeMic = null;
        _micStream = null;
        _musicSource = null;
        _scene?.Dispose();
        _scene = null;
        for (var i = _microphoneLabels.Count - 1; i >= 0; --i)
            _microphoneLabels[i].Dispose();
        _microphoneLabels.Clear();
        _microphones = [];
        base.Dispose();
    }

    private Scene? _scene;
    private IReadOnlyList<string> _microphones = [];
    private readonly List<CString> _microphoneLabels = [];
    private ImFont? _headingFont;
    private int _selectedMicrophone;

    /// <summary>Explicit pan value.</summary>
    private float _pan;
    /// <summary>Explicit reach value.</summary>
    private float _reach;
    /// <summary>Output to subwoofer.</summary>
    private bool _lfe;

    private Microphone? _activeMic;
    private BufferedSoundStream? _micStream;

    private static readonly Vector4f DashboardBackground = new(0.015f, 0.025f, 0.055f, 1.0f);
    private static readonly Vector4f PanelBackground = new(0.045f, 0.065f, 0.105f, 0.95f);
    private static readonly Vector4f PanelBorder = new(0.16f, 0.26f, 0.38f, 0.85f);
    private static readonly Vector4f ControlBackground = new(0.09f, 0.13f, 0.2f, 1.0f);
    private static readonly Vector4f ControlHovered = new(0.12f, 0.24f, 0.31f, 1.0f);
    private static readonly Vector4f ControlActive = new(0.1f, 0.36f, 0.42f, 1.0f);
    private static readonly Vector4f PrimaryText = new(0.92f, 0.97f, 1.0f, 1.0f);
    private static readonly Vector4f MutedText = new(0.52f, 0.62f, 0.72f, 1.0f);
    private static readonly Vector4f AccentCyan = new(0.15f, 0.93f, 1.0f, 1.0f);
    private static readonly Vector4f AccentViolet = new(0.68f, 0.48f, 1.0f, 1.0f);
    private static readonly Vector4f AccentAmber = new(1.0f, 0.68f, 0.25f, 1.0f);
    private static readonly Vector4f AccentRose = new(1.0f, 0.36f, 0.62f, 1.0f);
    private SampleInputMap? _sampleInput;
}
