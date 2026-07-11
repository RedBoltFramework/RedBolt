using System;
using System.IO;
using RedBolt;

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
    private SoundSource? _musicSource;
    private float _pan;
    private float _reach;
    private bool _lfe;
    private float _soundVolume = 1.0f;
    private float _musicVolume = 1.0f;

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.SoundEffects";
        parameters.GraphicsBackend = GraphicsBackend.OpenGL;
        parameters.ResourcePaths = "CoreData;Data;Sounds";
        parameters.ResourcePrefixPaths = FindResourcePrefixPath();
        parameters.WindowHeight = 540;
        parameters.WindowTitle = "RedBolt Sound Effects";
        parameters.WindowWidth = 960;
    }

    protected override void Start()
    {
        var audio = Context.GetSubsystem<Audio>();
        if (audio == null)
        {
            Console.Error.WriteLine("Audio subsystem not available.");
            Exit(1);
            return;
        }

        if (!audio.SetMode(100, 44100, SpeakerMode.Stereo))
        {
            Console.Error.WriteLine("Failed to initialize audio output.");
            Exit(1);
            return;
        }

        audio.SetMasterGain(AudioDefs.SoundEffect, _soundVolume);
        audio.SetMasterGain(AudioDefs.SoundMusic, _musicVolume);
        audio.Play();

        // Create music source
        _musicSource = new SoundSource();
        Console.WriteLine("SoundEffects sample ready.");
        Console.WriteLine("Keys: 1=Fist, 2=Explosion, 3=Power-up, M=Toggle music, +/-=Volume, P=Pan");
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape))
        {
            Exit();
            return;
        }

        var audio = Context.GetSubsystem<Audio>();
        if (audio == null) return;

        // Sound effects
        if (Input.GetKeyPress(Key.Num1))
            PlaySound("Sounds/PlayerFistHit.wav");
        if (Input.GetKeyPress(Key.Num2))
            PlaySound("Sounds/BigExplosion.wav");
        if (Input.GetKeyPress(Key.Num3))
            PlaySound("Sounds/Powerup.wav");

        // Music toggle
        if (Input.GetKeyPress(Key.M))
        {
            if (_musicSource != null && _musicSource.IsPlaying())
            {
                _musicSource.Stop();
                Console.WriteLine("Music stopped.");
            }
            else
            {
                var music = Context.GetSubsystem<ResourceCache>()?.GetResource<Sound>("Music/Ninja Gods.ogg");
                if (music != null)
                {
                    var node = new Scene().CreateChild();
                    _musicSource = node.CreateComponent<SoundSource>();
                    _musicSource.SetSoundType(AudioDefs.SoundMusic);
                    _musicSource.Play(music);
                    Console.WriteLine("Music playing.");
                }
            }
        }

        // Volume control
        if (Input.GetKeyPress(Key.Equals) || Input.GetKeyPress(Key.KpPlus))
        {
            _soundVolume = MathF.Min(_soundVolume + 0.1f, 1.0f);
            audio.SetMasterGain(AudioDefs.SoundEffect, _soundVolume);
            Console.WriteLine($"Sound volume: {_soundVolume:F1}");
        }
        if (Input.GetKeyPress(Key.Minus) || Input.GetKeyPress(Key.KpMinus))
        {
            _soundVolume = MathF.Max(_soundVolume - 0.1f, 0.0f);
            audio.SetMasterGain(AudioDefs.SoundEffect, _soundVolume);
            Console.WriteLine($"Sound volume: {_soundVolume:F1}");
        }

        // Panning
        if (Input.GetKeyDown(Key.P))
        {
            _pan = MathF.Sin(_pan + timeStep) * 0.5f;
        }
    }

    private void PlaySound(string resourceName)
    {
        var cache = Context.GetSubsystem<ResourceCache>();
        var sound = cache?.GetResource<Sound>(resourceName);
        if (sound == null)
        {
            Console.WriteLine($"Could not load sound: {resourceName}");
            return;
        }

        var node = new Scene().CreateChild();
        var source = node.CreateComponent<SoundSource>();
        source.SetSoundType(AudioDefs.SoundEffect);
        source.SetGain(_soundVolume);
        source.Play(sound);
    }

    protected override void Stop()
    {
        _musicSource?.Stop();
        Context.GetSubsystem<Audio>()?.Stop();
    }

    private static string FindResourcePrefixPath()
    {
        var envRoot = Environment.GetEnvironmentVariable("REDBOLT_RBFX_BIN_DIR");
        if (TryNormalizeResourcePrefixPath(envRoot, out var normalizedEnvRoot))
            return normalizedEnvRoot;

        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (TryNormalizeResourcePrefixPath(Path.Combine(current.FullName, "bin"), out var localBinRoot))
                return localBinRoot;
            if (TryNormalizeResourcePrefixPath(Path.Combine(current.FullName, "rbfx", "bin"), out var repoRbfxBinRoot))
                return repoRbfxBinRoot;
            if (TryNormalizeResourcePrefixPath(Path.Combine(current.FullName, "..", "rbfx", "bin"), out var siblingRbfxBinRoot))
                return siblingRbfxBinRoot;
            if (Directory.Exists(Path.Combine(current.FullName, ".git")))
                break;
        }
        throw new InvalidOperationException("SoundEffects sample could not locate rbfx resource directories.");
    }

    private static bool TryNormalizeResourcePrefixPath(string? root, out string normalizedRoot)
    {
        normalizedRoot = string.Empty;
        if (string.IsNullOrWhiteSpace(root)) return false;
        var candidateRoot = Path.GetFullPath(root);
        if (!Directory.Exists(Path.Combine(candidateRoot, "CoreData")) ||
            !Directory.Exists(Path.Combine(candidateRoot, "Data")))
            return false;
        normalizedRoot = candidateRoot;
        return true;
    }
}
