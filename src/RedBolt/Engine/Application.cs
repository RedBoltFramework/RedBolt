using Microsoft.Extensions.Logging;

namespace RedBolt;

public abstract class Application
{
    public const int FailureExitCode = 1;

    protected Application()
    {
        Engine = new Engine();
    }

    public int ExitCode { get; private set; }

    public string ErrorMessage { get; private set; } = string.Empty;

    public int MaxFrameCount { get; set; } = 10_000;

    protected Engine Engine { get; }

    protected ResourceCache Resources => GetRequiredSubsystem<ResourceCache>();

    protected Graphics Graphics => GetRequiredSubsystem<Graphics>();

    protected Renderer Renderer => GetRequiredSubsystem<Renderer>();

    protected Input Input => GetRequiredSubsystem<Input>();

    protected Context Context => Engine.Context;

    protected virtual void Setup(EngineParameters parameters)
    {
    }

    protected virtual void SetupCommandLine(CommandLineParser parser, EngineParameters parameters)
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
        return Run(Environment.GetCommandLineArgs().Skip(1).ToArray());
    }

    public int Run(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var parameters = new EngineParameters();
        Setup(parameters);

        if (parameters.EngineCliParameters)
        {
            EngineCommandLine.ApplyEnvironmentDefaults(parameters);

            var parser = new CommandLineParser(parameters.ApplicationName);
            EngineCommandLine.Register(parser, parameters);
            SetupCommandLine(parser, parameters);

            var parseResult = parser.Parse(args);
            if (parseResult.IsHelpRequested)
            {
                Console.Write(parser.GetUsage());
                return ExitCode;
            }

            if (!parseResult.IsSuccess)
            {
                ErrorExit(parseResult.ErrorMessage);
                Log.Logger.LogError(parseResult.ErrorMessage);
                Console.Error.WriteLine(parseResult.ErrorMessage);
                Console.Error.WriteLine();
                Console.Error.Write(parser.GetUsage());
                return ExitCode;
            }
        }

        Log.Initialize(parameters);

        if (ShouldStopBeforeInitialization())
            return ExitCode;

        var started = false;
        try
        {
            if (!Engine.Initialize(parameters))
            {
                ErrorExit();
                return ExitCode;
            }

            Start();
            started = true;

            if (!ShouldStopAfterStart())
            {
                var frameCount = 0;
                Action<float> updateAction = Update;
                while (!Engine.IsExiting)
                {
                    if (frameCount >= MaxFrameCount)
                    {
                        ErrorExit($"Application did not exit within {MaxFrameCount} frames.");
                        break;
                    }

                    Engine.RunFrame(updateAction);
                    ++frameCount;
                }
            }
        }
        catch (Exception exception)
        {
            Log.Logger.LogCritical(exception, "Unhandled exception in application run loop.");
            ErrorExit(exception.Message);
        }
        finally
        {
            if (started)
            {
                CaptureScreenshotOnExit();
                Stop();
            }

            if (Engine.IsInitialized)
                Engine.Shutdown();
            Log.Shutdown();
        }

        return ExitCode;
    }

    protected void Exit(int exitCode = 0)
    {
        ExitCode = exitCode;
        Engine.Exit();
    }

    protected void ErrorExit(string message = "")
    {
        ErrorMessage = string.IsNullOrWhiteSpace(message)
            ? "Application has been terminated due to unexpected error."
            : message;
        Log.Logger.LogError(ErrorMessage);
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

    private void CaptureScreenshotOnExit()
    {
        var screenshotPath = Engine.Parameters.ScreenshotOnExit;
        if (Engine.IsHeadless || string.IsNullOrWhiteSpace(screenshotPath) || !Graphics.IsInitialized)
            return;

        try
        {
            if (Renderer.GetNumViews() > 0)
                Renderer.RenderFrame(Graphics, screenshotPath: screenshotPath);
            else
                Graphics.SaveScreenshot(screenshotPath);
        }
        catch (Exception exception)
        {
            Log.Logger.LogWarning(exception, "Failed to save screenshot to '{ScreenshotPath}'.", screenshotPath);
        }
    }

    private T GetRequiredSubsystem<T>()
        where T : class
    {
        return Context.GetSubsystem<T>()
            ?? throw new InvalidOperationException($"Required subsystem {typeof(T).Name} is not registered in Context.");
    }
}
