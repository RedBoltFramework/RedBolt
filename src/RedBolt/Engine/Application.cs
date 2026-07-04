namespace RedBolt;

public abstract class Application
{
    public const int FailureExitCode = 1;

    protected Application()
    {
        Engine = new Engine();
    }

    public int ExitCode { get; private set; }

    public string? ErrorMessage { get; private set; }

    public int MaxFrameCount { get; set; } = 10_000;

    protected Engine Engine { get; }

    protected ResourceCache Resources => Engine.Resources;

    protected Graphics Graphics => Engine.Graphics;

    protected Renderer Renderer => Engine.Renderer;

    protected Input Input => Engine.Input;

    protected virtual void Setup(EngineParameters parameters)
    {
    }

    protected virtual void Start()
    {
    }

    protected virtual void Update(float timeStep)
    {
    }

    protected virtual void Stop()
    {
    }

    public int Run()
    {
        var parameters = new EngineParameters();
        Setup(parameters);

        if (ShouldStopBeforeInitialization())
            return ExitCode;

        if (!Engine.Initialize(parameters))
        {
            ErrorExit();
            return ExitCode;
        }

        var started = false;
        try
        {
            Start();
            started = true;

            if (!ShouldStopAfterStart())
            {
                var frameCount = 0;
                while (!Engine.IsExiting)
                {
                    if (frameCount >= MaxFrameCount)
                    {
                        ErrorExit($"Application did not exit within {MaxFrameCount} frames.");
                        break;
                    }

                    Engine.RunFrame(Update);
                    ++frameCount;
                }
            }
        }
        finally
        {
            if (started)
                Stop();

            Engine.Shutdown();
        }

        return ExitCode;
    }

    protected void Exit(int exitCode = 0)
    {
        ExitCode = exitCode;
        Engine.Exit();
    }

    protected void ErrorExit(string? message = null)
    {
        ErrorMessage = string.IsNullOrWhiteSpace(message)
            ? "Application has been terminated due to unexpected error."
            : message;
        Exit(FailureExitCode);
    }

    private bool ShouldStopBeforeInitialization()
    {
        return ExitCode != 0 || Engine.IsExiting;
    }

    private bool ShouldStopAfterStart()
    {
        return ExitCode != 0 || Engine.IsExiting;
    }
}
