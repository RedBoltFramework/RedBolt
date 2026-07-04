namespace RedBolt;

public sealed class Engine
{
    private readonly System.Diagnostics.Stopwatch frameClock = new();

    public EngineParameters Parameters { get; private set; } = new();

    public EngineTime Time { get; } = new();

    public ResourceCache Resources { get; } = new();

    public InputSystem Input { get; } = new();

    public GraphicsSystem Graphics { get; } = new();

    public bool IsInitialized { get; private set; }

    public bool IsExiting { get; private set; }

    public bool IsHeadless { get; private set; }

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

        var timeStep = (float)frameClock.Elapsed.TotalSeconds;
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
            if (Input.ExitRequested)
                Exit();
        }

        if (!IsExiting)
            update(timeStep);

        if (!IsHeadless && !IsExiting)
            Graphics.RenderFrame();
    }

    public void Exit()
    {
        IsExiting = true;
    }

    public void Shutdown()
    {
        if (!IsInitialized)
            return;

        Graphics.Shutdown();
        Input.Shutdown();
        IsInitialized = false;
        IsExiting = false;
        frameClock.Reset();
    }
}
