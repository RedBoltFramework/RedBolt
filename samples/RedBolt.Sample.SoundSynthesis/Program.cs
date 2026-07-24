using System;
using RedBolt;
using RedBolt.Bindings.DearImGui;

#if !REDBOLT_WEB_SAMPLE
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
#endif

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

        // Queue buffer to the stream for playback
        _soundStream.AddData(_soundData, checked((uint)(numSamples * sizeof(short))));
    }

    /// <summary>Construct an instruction text to the UI.</summary>
    private void CreateInstructions()
    {
        Context.RegisterSubsystem(new DearImGui((ImGuiConfigFlags)Engine.Parameters.DearImGuiFlags));
        _instructionLine = new CString();
        SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>((ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => DrawInstructions());

        // Construct new Text object, set string to display and font to use
        // Position the text relative to the screen center
        // The rbfx Text control is replaced by the allowed Dear ImGui sample UI surface.
    }

    /// <summary>Subscribe to application-wide logic update events.</summary>
    private void SubscribeToEvents()
    {
    }

    /// <summary>Handle the logic update event.</summary>
    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape))
        {
            Exit();
            return;
        }

        // Use keys to control the filter constant
        if (Input.GetKeyDown(Key.Up))
            _filter += timeStep * 0.5f;
        if (Input.GetKeyDown(Key.Down))
            _filter -= timeStep * 0.5f;
        _filter = MathDefs.Clamp(_filter, 0.01f, 1.0f);

        _instructionLine!.FormatInPlace($"Use cursor up and down to control sound filtering\nCoefficient: {_filter}");

        UpdateSound();
    }

    private void DrawInstructions()
    {
        var windowOpen = true;
        if (ImGui.Begin("Sound Synthesis\0"u8, ref windowOpen, ImGuiWindowFlags.ImGuiWindowFlagsNoResize | ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
            ImGui.TextUnformatted(_instructionLine!.Bytes);
        ImGui.End();
    }

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.SoundSynthesis";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.WindowMaximize = false;
        parameters.ResourcePaths = "CoreData;Data";
        parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowHeight = 540;
        parameters.WindowTitle = "RedBolt Sound Synthesis";
        parameters.WindowWidth = 960;
    }

    protected override void Stop()
    {
        _instructionLine?.Dispose();
        _instructionLine = null;
        _soundStream = null;
        _node?.Dispose();
        _node = null;
        base.Stop();
    }

    public override void Dispose()
    {
        _instructionLine?.Dispose();
        _instructionLine = null;
        _soundStream = null;
        _node?.Dispose();
        _node = null;
        base.Dispose();
    }


    /// <summary>Scene node for the sound component.</summary>
    private Node? _node;
    /// <summary>Sound stream that we update.</summary>
    private BufferedSoundStream? _soundStream;
    /// <summary>Instruction text.</summary>
    private CString? _instructionLine;
    /// <summary>Filter coefficient for the sound.</summary>
    private float _filter;
    /// <summary>Synthesis accumulator.</summary>
    private float _accumulator;
    /// <summary>First oscillator.</summary>
    private float _osc1;
    /// <summary>Second oscillator.</summary>
    private float _osc2;
}
