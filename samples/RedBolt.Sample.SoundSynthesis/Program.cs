using System;
using System.IO;
using RedBolt;

try
{
    var exitCode = new SoundSynthesisApplication().Run();
    if (exitCode == 0)
        Console.WriteLine("RedBolt SoundSynthesis sample finished.");
    return exitCode;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}

sealed class SoundSynthesisApplication : Application
{
    private SoundSource? _soundSource;
    private BufferedSoundStream? _soundStream;
    private float _filter = 0.5f;
    private float _accumulator;
    private float _osc1;
    private float _osc2;

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.SoundSynthesis";
        parameters.GraphicsBackend = GraphicsBackend.OpenGL;
        parameters.ResourcePaths = "CoreData;Data";
        parameters.ResourcePrefixPaths = FindResourcePrefixPath();
        parameters.WindowHeight = 540;
        parameters.WindowTitle = "RedBolt Sound Synthesis";
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

        if (!audio.SetMode(100, 44100, SpeakerMode.Mono))
        {
            Console.Error.WriteLine("Failed to initialize audio output.");
            Exit(1);
            return;
        }

        audio.Play();

        // Create a buffered sound stream for synthesized audio
        _soundStream = new BufferedSoundStream();
        _soundStream.SetFormat(44100, true, false);

        var node = new Scene().CreateChild();
        _soundSource = node.CreateComponent<SoundSource>();
        _soundSource.Play(_soundStream);

        Console.WriteLine("SoundSynthesis sample ready.");
        Console.WriteLine("Keys: UP=Increase filter, DOWN=Decrease filter, ESC=Exit");
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape))
        {
            Exit();
            return;
        }

        if (Input.GetKeyPress(Key.Up))
        {
            _filter = MathF.Min(_filter + 0.05f, 1.0f);
            Console.WriteLine($"Filter: {_filter:F2}");
        }
        if (Input.GetKeyPress(Key.Down))
        {
            _filter = MathF.Max(_filter - 0.05f, 0.0f);
            Console.WriteLine($"Filter: {_filter:F2}");
        }

        UpdateSound();
    }

    private void UpdateSound()
    {
        if (_soundStream == null) return;

        // Generate ~100ms of audio data (44100 Hz * 0.1s = 4410 samples, 16-bit mono = 8820 bytes)
        var sampleCount = 4410;
        var samples = new short[sampleCount];

        for (var i = 0; i < sampleCount; i++)
        {
            // Two oscillators at slightly different frequencies
            _osc1 += 0.05f;
            _osc2 += 0.051f;

            var sample = MathF.Sin(_osc1) * 0.5f + MathF.Sin(_osc2) * 0.3f;

            // Apply low-pass filter
            _accumulator = _accumulator * _filter + sample * (1.0f - _filter);

            // Clamp to 16-bit range and scale
            var value = _accumulator * 0.3f * short.MaxValue;
            samples[i] = (short)MathDefs.Clamp(value, short.MinValue, short.MaxValue);
        }

        var bytes = new byte[sampleCount * sizeof(short)];
        Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
        _soundStream.AddData(bytes, (uint)bytes.Length);
    }

    protected override void Stop()
    {
        _soundSource?.Stop();
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
        throw new InvalidOperationException("SoundSynthesis sample could not locate rbfx resource directories.");
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
