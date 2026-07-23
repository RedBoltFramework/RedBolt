using System;
using System.Collections.Generic;
using RedBolt;
using RedBolt.Bindings.DearImGui;

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
        Context.RegisterSubsystem(new DearImGui((ImGuiConfigFlags)Engine.Parameters.DearImGuiFlags));
        _dearImGuiSubscription = Context.SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>((ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => DrawUI());

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
        var windowOpen = true;
        if (ImGui.Begin("Sound Effects\0"u8, ref windowOpen, ImGuiWindowFlags.ImGuiWindowFlagsNoResize | ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            // Create buttons for playing back sounds
            if (ImGui.Button("Fist\0"u8))
                HandlePlaySound("Sounds/PlayerFistHit.wav");
            if (ImGui.Button("Explosion\0"u8))
                HandlePlaySound("Sounds/BigExplosion.wav");
            if (ImGui.Button("Power-up\0"u8))
                HandlePlaySound("Sounds/Powerup.wav");

            // Create buttons for playing/stopping music
            if (ImGui.Button("Play Music\0"u8))
                HandlePlayMusic();
            if (ImGui.Button("Stop Music\0"u8))
                HandleStopMusic();

            // Create buttons and slider for scene time control
            if (ImGui.Button("Pause Scene\0"u8))
                HandlePauseScene();
            if (ImGui.Button("Resume Scene\0"u8))
                HandleResumeScene();

            var sceneTimeScale = _scene!.GetTimeScale();
            if (ImGui.SliderFloat("Scene Time Scale\0"u8, out sceneTimeScale, 0.0f, 2.0f))
                HandleSceneTimeScale(sceneTimeScale);

            // Create sliders for controlling sound and music master volume
            var audio = GetSubsystem<Audio>()!;
            var soundVolume = audio.GetMasterGain(AudioDefs.SoundEffect);
            if (ImGui.SliderFloat("Sound Volume\0"u8, out soundVolume, 0.0f, 1.0f))
                HandleSoundVolume(soundVolume);
            var musicVolume = audio.GetMasterGain(AudioDefs.SoundMusic);
            if (ImGui.SliderFloat("Music Volume\0"u8, out musicVolume, 0.0f, 1.0f))
                HandleMusicVolume(musicVolume);

            var pan = (_pan + 1.0f) * 0.5f;
            if (ImGui.SliderFloat("Sound Panning\0"u8, out pan, 0.0f, 1.0f))
                HandleSoundPan(pan);

            var reach = (_reach + 1.0f) * 0.5f;
            if (ImGui.SliderFloat("Sound Reach\0"u8, out reach, 0.0f, 1.0f))
                HandleSoundReach(reach);

            var lfe = _lfe;
            if (ImGui.Checkbox("Output to LFE\0"u8, ref lfe))
                HandleLFE(lfe);

            for (var i = 0; i < _microphoneLabels.Count; ++i)
            {
                if (ImGui.Button(_microphoneLabels[i].Bytes))
                    _selectedMicrophone = i;
            }

            if (ImGui.Button("Start Record\0"u8))
                HandleStartMicRecord();
            if (ImGui.Button("Stop Record\0"u8))
                HandleStopMicRecord();
        }
        ImGui.End();
    }

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.SoundEffects";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.WindowMaximize = false;
        parameters.ResourcePaths = "CoreData;Data";
        parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowHeight = 540;
        parameters.WindowTitle = "RedBolt Sound Effects";
        parameters.WindowWidth = 960;
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape))
            Exit();
    }

    protected override void Stop()
    {
        _dearImGuiSubscription?.Dispose();
        _dearImGuiSubscription = null;
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
        _dearImGuiSubscription?.Dispose();
        _dearImGuiSubscription = null;
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
    private ContextEventSubscription? _dearImGuiSubscription;
    private IReadOnlyList<string> _microphones = [];
    private readonly List<CString> _microphoneLabels = [];
    private int _selectedMicrophone;

    /// <summary>Explicit pan value.</summary>
    private float _pan;
    /// <summary>Explicit reach value.</summary>
    private float _reach;
    /// <summary>Output to subwoofer.</summary>
    private bool _lfe;

    private Microphone? _activeMic;
    private BufferedSoundStream? _micStream;
}
