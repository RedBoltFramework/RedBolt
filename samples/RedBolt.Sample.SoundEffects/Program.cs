using System;
using System.IO;
using RedBolt;
using RedBolt.Bindings.DearImGui;

try
{
    var exitCode = new SoundEffectsApplication().Run();
    if (exitCode == 0)
        Console.WriteLine("RedBolt SoundEffects sample finished.");
    return exitCode;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}

sealed class SoundEffectsApplication : Application
{
    private Scene? _scene;
    private SoundSource? _musicSource;
    private float _pan;
    private float _reach;
    private bool _lfe;
    private float _soundVolume = 1.0f;
    private float _musicVolume = 1.0f;
    private ContextEventSubscription? _dearImGuiSubscription;
    private Microphone? _activeMic;
    private BufferedSoundStream? _micStream;

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.SoundEffects";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.WindowMaximize = false;
        parameters.ResourcePaths = "CoreData;Data";
        parameters.ResourcePrefixPaths = FindResourcePrefixPath();
        parameters.WindowHeight = 540;
        parameters.WindowTitle = "RedBolt Sound Effects";
        parameters.WindowWidth = 960;
    }

    protected override void Start()
    {
        var audio = GetSubsystem<Audio>()!;

        if (!audio.SetMode(100, 44100, SpeakerMode.Stereo))
        {
            Console.Error.WriteLine("Failed to initialize audio output.");
            Exit(1);
            return;
        }

        audio.SetMasterGain(AudioDefs.SoundEffect, _soundVolume);
        audio.SetMasterGain(AudioDefs.SoundMusic, _musicVolume);
        audio.Play();

        Context.RegisterSubsystem(new DearImGui(Context, Engine.Parameters.DearImGuiFlags));
        _dearImGuiSubscription = Context.SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>((ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => DrawUI());

        _scene = new Scene();
        _scene.CreateComponent<RenderPipeline>();
        _scene.CreateComponent<Octree>();
        var cameraNode = _scene.CreateChild("Camera");
        cameraNode.SetPosition(new Vector3f(0.0f, 0.0f, -4.0f));
        var camera = cameraNode.CreateComponent<Camera>();
        Renderer.SetViewport(0, new Viewport(_scene, camera));
        _musicSource = _scene.CreateChild().CreateComponent<SoundSource>();
        _musicSource.SetSoundType(AudioDefs.SoundMusic);
        PlaySound("Sounds/PlayerFistHit.wav");
        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(true);
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape))
        {
            Exit();
            return;
        }

    }

    private void PlaySound(string resourceName)
    {
        var sound = GetSubsystem<ResourceCache>()!.GetResource<Sound>(resourceName);
        if (sound == null)
        {
            Console.WriteLine($"Could not load sound: {resourceName}");
            return;
        }

        var source = _scene!.CreateChild().CreateComponent<SoundSource>();
        source.SetAutoRemoveMode(AutoRemoveMode.Component);
        source.Play(sound);
        source.SetGain(0.75f);
        source.SetPanning(_pan);
        source.SetReach(_reach);
        source.SetLowFrequency(_lfe);
    }

    protected override void Stop()
    {
        _dearImGuiSubscription?.Dispose();
        _dearImGuiSubscription = null;
        _activeMic = null;
        _micStream = null;
        GetSubsystem<Audio>()!.Stop();
    }

    private void DrawUI()
    {
        if (ImGui.Begin("Sound Effects\0"u8, out _, ImGuiWindowFlags.ImGuiWindowFlagsNoResize | ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            if (ImGui.Button("Fist\0"u8))
                PlaySound("Sounds/PlayerFistHit.wav");
            if (ImGui.Button("Explosion\0"u8))
                PlaySound("Sounds/BigExplosion.wav");
            if (ImGui.Button("Power-up\0"u8))
                PlaySound("Sounds/Powerup.wav");

            if (ImGui.Button("Play Music\0"u8))
                PlayMusic();
            if (ImGui.Button("Stop Music\0"u8))
                _musicSource!.Stop();

            if (ImGui.Button("Pause Scene\0"u8))
                _scene!.SetUpdateEnabled(false);
            if (ImGui.Button("Resume Scene\0"u8))
                _scene!.SetUpdateEnabled(true);

            var sceneTimeScale = _scene!.GetTimeScale();
            if (ImGui.SliderFloat("Scene Time Scale\0"u8, out sceneTimeScale, 0.0f, 2.0f))
                _scene.SetTimeScale(sceneTimeScale);

            if (ImGui.SliderFloat("Sound Volume\0"u8, out _soundVolume, 0.0f, 1.0f))
                GetSubsystem<Audio>()!.SetMasterGain(AudioDefs.SoundEffect, _soundVolume);
            if (ImGui.SliderFloat("Music Volume\0"u8, out _musicVolume, 0.0f, 1.0f))
                GetSubsystem<Audio>()!.SetMasterGain(AudioDefs.SoundMusic, _musicVolume);

            var pan = (_pan + 1.0f) * 0.5f;
            if (ImGui.SliderFloat("Sound Panning\0"u8, out pan, 0.0f, 1.0f))
                _pan = pan * 2.0f - 1.0f;

            var reach = (_reach + 1.0f) * 0.5f;
            if (ImGui.SliderFloat("Sound Reach\0"u8, out reach, 0.0f, 1.0f))
                _reach = reach * 2.0f - 1.0f;

            if (ImGui.Checkbox("Output to LFE\0"u8, out var lfe))
                _lfe = lfe;

            if (ImGui.Button("Start Record\0"u8))
                StartMicRecord();
            if (ImGui.Button("Stop Record\0"u8))
                StopMicRecord();
        }
        ImGui.End();
    }

    private void PlayMusic()
    {
        var music = GetSubsystem<ResourceCache>()!.GetResource<Sound>("Music/Ninja Gods.ogg");
        music.SetLooped(true);
        _musicSource!.Play(music);
    }

    private void StartMicRecord()
    {
        if (_activeMic != null)
            return;

        var microphones = GetSubsystem<Audio>()!.EnumerateMicrophones();
        if (microphones.Count == 0)
            return;

        _activeMic = GetSubsystem<Audio>()!.CreateMicrophone(microphones[0], false, 16000, 64);
        if (_activeMic != null)
        {
            _micStream = new BufferedSoundStream();
            _micStream.SetFormat(_activeMic.GetFrequency(), sixteenBit: true, stereo: false);
            _activeMic.Link(_micStream);
        }
    }

    private void StopMicRecord()
    {
        _activeMic = null;
        if (_micStream == null)
            return;

        var source = _scene!.CreateChild().CreateComponent<SoundSource>();
        source.SetAutoRemoveMode(AutoRemoveMode.Component);
        source.Play(_micStream);
        source.SetGain(1.0f);
        source.SetPanning(0.0f);
        source.SetReach(0.0f);
    }

    private static string FindResourcePrefixPath()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            var prefix = Path.Combine(current.FullName, "bin");
            if (Path.Exists(Path.Combine(prefix, "CoreData")))
                return prefix;
        }
        throw new InvalidOperationException("SoundEffects sample could not locate rbfx resource directories.");
    }
}
