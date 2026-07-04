namespace RedBolt;

public sealed class Engine
{
    private readonly Dictionary<string, object?> customParameters = new(StringComparer.Ordinal);
    private readonly System.Diagnostics.Stopwatch frameClock = new();
    private float? nextTimeStep;

    public EngineParameters Parameters { get; private set; } = new();

    public EngineTime Time { get; } = new();

    public ResourceCache Resources { get; } = new();

    public Input Input { get; } = new();

    public Graphics Graphics { get; } = new();

    public Renderer Renderer { get; } = new();

    public bool IsInitialized { get; private set; }

    public bool IsExiting { get; private set; }

    public bool IsHeadless { get; private set; }

    public bool AutoExit { get; private set; } = true;

    public int MaxFps { get; private set; } = 200;

    public int MaxInactiveFps { get; private set; } = 60;

    public int MinFps { get; private set; } = 10;

    public bool PauseMinimized { get; private set; } = true;

    public int TimeStepSmoothing { get; private set; } = 2;

    public bool Initialize(EngineParameters applicationParameters)
    {
        ArgumentNullException.ThrowIfNull(applicationParameters);

        if (IsInitialized)
            throw new InvalidOperationException("Engine is already initialized.");

        Parameters = applicationParameters.Clone();
        IsHeadless = Parameters.Headless;
        Resources.RegisterDefaultResources();

        if (!IsHeadless)
        {
            Input.Initialize(Parameters);
            try
            {
                Graphics.Initialize(Parameters, Input);
            }
            catch
            {
                Input.Shutdown();
                throw;
            }
        }

        IsInitialized = true;
        IsExiting = false;
        frameClock.Restart();
        return true;
    }

    public void RunFrame(Action<float> update)
    {
        ArgumentNullException.ThrowIfNull(update);

        if (!IsInitialized)
            throw new InvalidOperationException("Engine must be initialized before running frames.");

        var timeStep = nextTimeStep ?? (float)frameClock.Elapsed.TotalSeconds;
        nextTimeStep = null;
        frameClock.Restart();
        if (timeStep < 0.0f)
            timeStep = 0.0f;
        else if (timeStep > 0.25f)
            timeStep = 0.25f;

        Time.Advance(timeStep);

        if (!IsHeadless)
        {
            Input.BeginFrame();
            Input.PollEvents();
            if (Input.ExitRequested && AutoExit)
                Exit();
        }

        if (!IsExiting)
            update(timeStep);

        if (!IsHeadless && !IsExiting)
            Renderer.RenderFrame(Graphics);
    }

    public void Exit()
    {
        IsExiting = true;
    }

    public void SetMinFps(int fps)
    {
        MinFps = Math.Max(fps, 1);
    }

    public int GetMinFps() => MinFps;

    public void SetMaxFps(int fps)
    {
        MaxFps = Math.Max(fps, 0);
    }

    public int GetMaxFps() => MaxFps;

    public void SetMaxInactiveFps(int fps)
    {
        MaxInactiveFps = Math.Max(fps, 0);
    }

    public int GetMaxInactiveFps() => MaxInactiveFps;

    public void SetTimeStepSmoothing(int frames)
    {
        TimeStepSmoothing = Math.Max(frames, 1);
    }

    public int GetTimeStepSmoothing() => TimeStepSmoothing;

    public void SetPauseMinimized(bool enable)
    {
        PauseMinimized = enable;
    }

    public bool GetPauseMinimized() => PauseMinimized;

    public void SetAutoExit(bool enable)
    {
        AutoExit = enable;
    }

    public bool GetAutoExit() => AutoExit;

    public void SetNextTimeStep(float seconds)
    {
        nextTimeStep = Math.Max(seconds, 0.0f);
    }

    public float GetNextTimeStep() => nextTimeStep ?? Time.TimeStep;

    public void SetParameter(string name, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        customParameters[name] = value;
    }

    public bool HasParameter(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return customParameters.ContainsKey(name);
    }

    public object? GetParameter(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return customParameters.GetValueOrDefault(name);
    }

    public void Shutdown()
    {
        if (!IsInitialized)
            return;

        Graphics.Shutdown();
        Renderer.ClearViewports();
        Input.Shutdown();
        IsInitialized = false;
        IsExiting = false;
        nextTimeStep = null;
        frameClock.Reset();
    }
}
