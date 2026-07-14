using System;
using System.IO;
using RedBolt;
using RedBolt.Bindings.DearImGui;

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
    private float _osc2 = 180.0f;
    private readonly CString _instructionLine = new();
    private ContextEventSubscription? _dearImGuiSubscription;

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
        var audio = GetSubsystem<Audio>()!;

        if (!audio.SetMode(100, 44100, SpeakerMode.Mono))
        {
            Console.Error.WriteLine("Failed to initialize audio output.");
            Exit(1);
            return;
        }

        audio.Play();
        Context.RegisterSubsystem(new DearImGui(Context, Engine.Parameters.DearImGuiFlags));
        _dearImGuiSubscription = Context.SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>((ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => DrawInstructions());

        // Create a buffered sound stream for synthesized audio
        _soundStream = new BufferedSoundStream();
        _soundStream.SetFormat(44100, true, false);

        var node = new Scene().CreateChild();
        _soundSource = node.CreateComponent<SoundSource>();
        _soundSource.Play(_soundStream);

        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(true);
        Console.WriteLine("SoundSynthesis sample ready.");
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape))
        {
            Exit();
            return;
        }

        if (Input.GetKeyDown(Key.Up))
            _filter += timeStep * 0.5f;
        if (Input.GetKeyDown(Key.Down))
            _filter -= timeStep * 0.5f;
        _filter = MathDefs.Clamp(_filter, 0.01f, 1.0f);

        UpdateSound();
    }

    private void UpdateSound()
    {
        var targetLength = 1.0f / 10.0f;
        var requiredLength = targetLength - _soundStream!.GetBufferLength();
        if (requiredLength < 0.0f)
            return;

        var sampleCount = (uint)(_soundStream.GetFrequency() * requiredLength);
        if (sampleCount == 0)
            return;

        var samples = new short[sampleCount];

        for (var i = 0; i < sampleCount; i++)
        {
            _osc1 = (_osc1 + 1.0f) % 360.0f;
            _osc2 = (_osc2 + 1.002f) % 360.0f;

            var newValue = MathDefs.Clamp((MathDefs.Sin(_osc1) + MathDefs.Sin(_osc2)) * 100000.0f, -32767.0f, 32767.0f);
            _accumulator = MathDefs.Lerp(_accumulator, newValue, _filter);
            samples[i] = (short)_accumulator;
        }

        _soundStream.AddData(samples, sampleCount * (uint)sizeof(short));
    }

    protected override void Stop()
    {
        _dearImGuiSubscription?.Dispose();
        _dearImGuiSubscription = null;
        _instructionLine.Dispose();
        _soundSource?.Stop();
        GetSubsystem<Audio>()!.Stop();
    }

    private void DrawInstructions()
    {
        _instructionLine.FormatInPlace($"Use cursor up and down to control sound filtering\nCoefficient: {_filter}");
        if (ImGui.Begin("Sound Synthesis\0"u8, out _, ImGuiWindowFlags.ImGuiWindowFlagsNoResize | ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
            ImGui.TextUnformatted(_instructionLine.Bytes);
        ImGui.End();
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
